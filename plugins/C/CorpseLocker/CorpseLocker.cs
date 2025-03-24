using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Corpse Locker", "WhiteThunder", "1.0.2")]
    [Description("Adds UI buttons to player corpses to allow quick looting.")]
    internal class CorpseLocker : CovalencePlugin
    {
        #region Fields

        private const string PermissionUse = "corpselocker.use";

        private Configuration _config;
        private ContainerManager _containerManager;
        private List<Item> _tempItemList = new List<Item>();

        public CorpseLocker()
        {
            _containerManager = new ContainerManager(this);
        }

        #endregion

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(PermissionUse, this);
        }

        private void OnServerInitialized()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                var container = player.inventory.loot.containers.FirstOrDefault();
                if (container == null)
                    continue;

                var corpse = container.entityOwner as PlayerCorpse;
                if ((object)corpse == null)
                    continue;

                OnLootEntity(player, corpse);
            }
        }

        private void Unload()
        {
            _containerManager.Unload();
        }

        private void OnLootEntity(BasePlayer looter, PlayerCorpse corpse)
        {
            if (IsCorpseAllowed(corpse, looter) && HasPermission(looter, PermissionUse))
            {
                var corpse2 = corpse;
                var looter2 = looter;

                NextTick(() =>
                {
                    if (!AreValidCorpseContainers(corpse2, looter2.inventory.loot.containers))
                        return;

                    _containerManager.AddCorpseLooter(corpse2, looter2);
                });
            }
        }

        #endregion

        #region Commands

        [Command("corpselocker.take.main")]
        private void CommandTakeMain(IPlayer player)
        {
            HandleTransfer(player, InventoryType.Main);
        }

        [Command("corpselocker.take.clothing")]
        private void CommandTakeClothing(IPlayer player)
        {
            HandleTransfer(player, InventoryType.Wear);
        }

        [Command("corpselocker.take.belt")]
        private void CommandTakeBelt(IPlayer player)
        {
            HandleTransfer(player, InventoryType.Belt);
        }

        [Command("corpselocker.swap.clothing")]
        private void CommandSwapClothing(IPlayer player)
        {
            HandleTransfer(player, InventoryType.Wear, swap: true);
        }

        [Command("corpselocker.swap.belt")]
        private void CommandSwapBelt(IPlayer player)
        {
            HandleTransfer(player, InventoryType.Belt, swap: true);
        }

        #endregion

        #region UI

        [Flags]
        private enum InventoryType
        {
            Main = 1 << 0,
            Belt = 1 << 1,
            Wear = 1 << 2,
        }

        private class CuiElementRecreate : CuiElement
        {
            [JsonProperty("destroyUi", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public string DestroyUi { get; set; }
        }

        private static class UI
        {
            private static Dictionary<string, Dictionary<InventoryType, string>> _uiCacheByLanguage =
                new Dictionary<string, Dictionary<InventoryType, string>>();

            public const string Name = "CorpseLocker";

            private const float PanelEndX = 572.5f;
            private const float ButtonSpacingX = 4;
            private const float RightButtonMinX = PanelEndX - ButtonWidth;
            private const float LeftButtonMinX = PanelEndX - 2 * ButtonWidth - ButtonSpacingX;
            private const float ButtonWidth = 70;
            private const float ButtonHeight = 21;

            public static void AddCorpseUI(CorpseLocker plugin, BasePlayer player, InventoryType inventoryTypes)
            {
                var lang = plugin.lang.GetLanguage(player.UserIDString);

                Dictionary<InventoryType, string> jsonByInventoryTypes;
                if (!_uiCacheByLanguage.TryGetValue(lang, out jsonByInventoryTypes))
                {
                    jsonByInventoryTypes = new Dictionary<InventoryType, string>();
                    _uiCacheByLanguage[lang] = jsonByInventoryTypes;
                }

                string cachedJson;
                if (!jsonByInventoryTypes.TryGetValue(inventoryTypes, out cachedJson))
                {
                    var cuiElements = CreatePanel();

                    if (inventoryTypes.HasFlag(InventoryType.Main))
                    {
                        AddButton(cuiElements,  plugin.GetMessage(player.UserIDString, LangEntry.TakeItems), "corpselocker.take.main", RightButtonMinX, 585.5f);
                    }

                    if (inventoryTypes.HasFlag(InventoryType.Wear))
                    {
                        AddButton(cuiElements, plugin.GetMessage(player.UserIDString, LangEntry.SwapItems), "corpselocker.swap.clothing", LeftButtonMinX, 308.5f);
                        AddButton(cuiElements, plugin.GetMessage(player.UserIDString, LangEntry.TakeItems), "corpselocker.take.clothing", RightButtonMinX, 308.5f);
                    }

                    if (inventoryTypes.HasFlag(InventoryType.Belt))
                    {
                        AddButton(cuiElements, plugin.GetMessage(player.UserIDString, LangEntry.SwapItems), "corpselocker.swap.belt", LeftButtonMinX, 175.5f);
                        AddButton(cuiElements, plugin.GetMessage(player.UserIDString, LangEntry.TakeItems), "corpselocker.take.belt", RightButtonMinX, 175.5f);
                    }

                    cachedJson = CuiHelper.ToJson(cuiElements);
                    jsonByInventoryTypes[inventoryTypes] = cachedJson;
                }

                CuiHelper.AddUi(player, cachedJson);
            }

            private static CuiElementContainer CreatePanel()
            {
                return new CuiElementContainer
                {
                    new CuiElementRecreate
                    {
                        Parent = "Hud.Menu",
                        Name = Name,
                        DestroyUi = Name,
                        Components =
                        {
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5 0",
                                AnchorMax = "0.5 0",
                                OffsetMin = "0 0",
                                OffsetMax = "0 0",
                            }
                        }
                    }
                };
            }

            private static void AddButton(CuiElementContainer container, string text, string command, float offsetX, float offsetY)
            {
                container.Add(new CuiButton
                {
                    Text =
                    {
                        Text = text,
                        Align = TextAnchor.MiddleCenter,
                        Color = "0.659 0.918 0.2 1"
                    },
                    Button =
                    {
                        Command = command,
                        Color = "0.451 0.553 0.271 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.5 0",
                        AnchorMax = "0.5 0",
                        OffsetMin = $"{offsetX} {offsetY}",
                        OffsetMax = $"{offsetX + ButtonWidth} {offsetY + ButtonHeight}",
                    }
                }, Name);
            }
        }

        #endregion

        #region Unity Component

        private class CorpseComponent : FacepunchBehaviour
        {
            public static CorpseComponent AddToCorpse(ContainerManager containerManager, PlayerCorpse corpse)
            {
                var component = corpse.gameObject.AddComponent<CorpseComponent>();
                component._containerManager = containerManager;
                component._corpse = corpse;
                component._activeInventoryTypes = component.DetermineActiveInventoryTypes();

                var handleDirtyDelayed = new Action(component.HandleDirtyDelayed);
                component._handleDirty = () => component.Invoke(handleDirtyDelayed, 0);

                foreach (var container in corpse.containers)
                {
                    container.onDirty += component._handleDirty;
                }

                return component;
            }

            private ContainerManager _containerManager;
            private PlayerCorpse _corpse;
            private List<BasePlayer> _looters = new List<BasePlayer>();
            private Action _handleDirty;
            private InventoryType _activeInventoryTypes;

            public void AddLooter(BasePlayer looter)
            {
                _looters.Add(looter);

                if (_activeInventoryTypes != 0)
                {
                    AddUI(looter);
                }
            }

            private void AddUI(BasePlayer looter)
            {
                UI.AddCorpseUI(_containerManager.Plugin, looter, _activeInventoryTypes);
            }

            private void DestroyUI(BasePlayer looter)
            {
                CuiHelper.DestroyUi(looter, UI.Name);
            }

            private InventoryType DetermineActiveInventoryTypes()
            {
                InventoryType inventoryTypes = 0;

                if (!GetCorpseContainer(_corpse, InventoryType.Main).IsEmpty())
                {
                    inventoryTypes |= InventoryType.Main;
                }

                if (!GetCorpseContainer(_corpse, InventoryType.Belt).IsEmpty())
                {
                    inventoryTypes |= InventoryType.Belt;
                }

                if (!GetCorpseContainer(_corpse, InventoryType.Wear).IsEmpty())
                {
                    inventoryTypes |= InventoryType.Wear;
                }

                return inventoryTypes;
            }

            public void AddUI()
            {
                foreach (var looter in _looters)
                {
                    AddUI(looter);
                }
            }

            public void DestroyUI()
            {
                foreach (var looter in _looters)
                {
                    DestroyUI(looter);
                }
            }

            private void HandleDirtyDelayed()
            {
                var inventoryTypes = DetermineActiveInventoryTypes();

                if (_activeInventoryTypes == inventoryTypes)
                    return;

                _activeInventoryTypes = inventoryTypes;

                if (inventoryTypes == 0)
                {
                    DestroyUI();
                }
                else
                {
                    AddUI();
                }
            }

            private void PlayerStoppedLooting(BasePlayer looter)
            {
                DestroyUI(looter);
                _looters.Remove(looter);
            }

            private void OnDestroy()
            {
                if (_activeInventoryTypes != 0)
                {
                    DestroyUI();
                }

                if (_corpse.containers != null)
                {
                    foreach (var container in _corpse.containers)
                    {
                        container.onDirty -= _handleDirty;
                    }
                }

                _containerManager.Unregister(_corpse);
            }
        }

        private class ContainerManager
        {
            public CorpseLocker Plugin { get; }

            private Dictionary<PlayerCorpse, CorpseComponent> _corpseComponents = new Dictionary<PlayerCorpse, CorpseComponent>();

            public ContainerManager(CorpseLocker plugin)
            {
                Plugin = plugin;
            }

            public void AddCorpseLooter(PlayerCorpse corpse, BasePlayer looter)
            {
                EnsureComponent(corpse)?.AddLooter(looter);
            }

            private CorpseComponent EnsureComponent(PlayerCorpse corpse)
            {
                CorpseComponent corpseComponent;
                if (!_corpseComponents.TryGetValue(corpse, out corpseComponent))
                {
                    corpseComponent = CorpseComponent.AddToCorpse(this, corpse);
                    _corpseComponents[corpse] = corpseComponent;
                }

                return corpseComponent;
            }

            public void Unregister(PlayerCorpse corpse)
            {
                _corpseComponents.Remove(corpse);
            }

            public void Unload()
            {
                foreach (var component in _corpseComponents.Values.ToArray())
                {
                    UnityEngine.Object.DestroyImmediate(component);
                }
            }
        }

        #endregion

        #region Helper Methods

        private static bool VerifyLootingEntity<T>(IPlayer player, out BasePlayer looter, out T entity) where T : BaseEntity
        {
            if (player.IsServer)
            {
                looter = null;
                entity = null;
                return false;
            }

            looter = player.Object as BasePlayer;
            var containers = looter.inventory.loot.containers;

            entity = containers.FirstOrDefault()?.entityOwner as T;
            return (object)entity != null && !entity.IsDestroyed;
        }

        private static ItemContainer GetCorpseContainer(PlayerCorpse corpse, InventoryType inventoryType)
        {
            switch (inventoryType)
            {
                case InventoryType.Main:
                    return corpse.containers[0];

                case InventoryType.Wear:
                    return corpse.containers[1];

                case InventoryType.Belt:
                    return corpse.containers[2];

                default:
                    return null;
            }
        }

        private static ItemContainer GetPlayerContainer(BasePlayer player, InventoryType inventoryType)
        {
            switch (inventoryType)
            {
                case InventoryType.Main:
                    return player.inventory.containerMain;

                case InventoryType.Belt:
                    return player.inventory.containerBelt;

                case InventoryType.Wear:
                    return player.inventory.containerWear;

                default:
                    return null;
            }
        }

        private static bool AreValidCorpseContainers(PlayerCorpse corpse, List<ItemContainer> containers)
        {
            if (containers.Count != 3 || corpse.containers.Length != 3)
                return false;

            // Verify container belongs to the corpse, in case the corpse is being used as a facade.
            for (var i = 0; i < containers.Count; i++)
            {
                if (containers[i] != corpse.containers[i])
                    return false;
            }

            return true;
        }

        private void TransferItems(BasePlayer looter, ItemContainer corpseContainer, ItemContainer playerContainer, bool swap = false)
        {
            if (swap)
            {
                _tempItemList.Clear();

                for (var i = 0; i < playerContainer.capacity; i++)
                {
                    var item = playerContainer.GetSlot(i);
                    if (item == null)
                        continue;

                    item.RemoveFromContainer();
                    item.position = i;
                    _tempItemList.Add(item);
                }
            }

            for (var i = 0; i < corpseContainer.capacity; i++)
            {
                corpseContainer.GetSlot(i)?.MoveToContainer(playerContainer, swap ? i : -1);
            }

            if (swap)
            {
                foreach (var item in _tempItemList)
                {
                    if (!item.MoveToContainer(corpseContainer, item.position) && !item.MoveToContainer(corpseContainer))
                    {
                        looter.GiveItem(item);
                    }
                }

                _tempItemList.Clear();
            }
        }

        private bool IsCorpseAllowed(PlayerCorpse corpse, BasePlayer looter)
        {
            if (corpse.containers?.Length != 3)
                return false;

            if (!corpse.playerSteamID.IsSteamId())
                return false;

            if (_config.RequireCorpseOwnership && corpse.playerSteamID != looter.userID)
                return false;

            if (!HasPermission(looter, PermissionUse))
                return false;

            return true;
        }

        private void HandleTransfer(IPlayer player, InventoryType inventoryType, bool swap = false)
        {
            BasePlayer looter;
            PlayerCorpse corpse;
            if (!VerifyLootingEntity(player, out looter, out corpse))
                return;

            if (!IsCorpseAllowed(corpse, looter))
                return;

            if (!AreValidCorpseContainers(corpse, looter.inventory.loot.containers))
                return;

            var corpseContainer = GetCorpseContainer(corpse, inventoryType);
            var playerContainer = inventoryType == InventoryType.Main || !swap
                ? GetPlayerContainer(looter, InventoryType.Main)
                : GetPlayerContainer(looter, inventoryType);

            TransferItems(looter, corpseContainer, playerContainer, swap);
        }

        private bool HasPermission(BasePlayer player, string perm)
        {
            return permission.UserHasPermission(player.UserIDString, perm);
        }

        #endregion

        #region Configuration

        [JsonObject(MemberSerialization.OptIn)]
        private class Configuration : BaseConfiguration
        {
            [JsonProperty("Require corpse ownership")]
            public bool RequireCorpseOwnership = true;
        }

        private Configuration GetDefaultConfig() => new Configuration();

        #region Configuration Helpers

        [JsonObject(MemberSerialization.OptIn)]
        private class BaseConfiguration
        {
            private string ToJson() => JsonConvert.SerializeObject(this);

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
            return MaybeUpdateConfigSection(currentWithDefaults, currentRaw);
        }

        private bool MaybeUpdateConfigSection(Dictionary<string, object> currentWithDefaults, Dictionary<string, object> currentRaw)
        {
            bool changed = false;

            foreach (var key in currentWithDefaults.Keys)
            {
                object currentRawValue;
                if (currentRaw.TryGetValue(key, out currentRawValue))
                {
                    var defaultDictValue = currentWithDefaults[key] as Dictionary<string, object>;
                    var currentDictValue = currentRawValue as Dictionary<string, object>;

                    if (defaultDictValue != null)
                    {
                        if (currentDictValue == null)
                        {
                            currentRaw[key] = currentWithDefaults[key];
                            changed = true;
                        }
                        else if (MaybeUpdateConfigSection(defaultDictValue, currentDictValue))
                            changed = true;
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
                    PrintWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch (Exception e)
            {
                PrintError(e.Message);
                PrintWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Puts($"Configuration changes saved to {Name}.json");
            Config.WriteObject(_config, true);
        }

        #endregion

        #endregion

        #region Localization

        private class LangEntry
        {
            public static List<LangEntry> AllLangEntries = new List<LangEntry>();

            public static readonly LangEntry TakeItems = new LangEntry("UI.Take", "Take");
            public static readonly LangEntry SwapItems = new LangEntry("UI.Swap", "Swap");

            public string Name;
            public string English;

            public LangEntry(string name, string english)
            {
                Name = name;
                English = english;

                AllLangEntries.Add(this);
            }
        }

        private string GetMessage(string playerId, LangEntry langEntry) =>
            lang.GetMessage(langEntry.Name, this, playerId);

        protected override void LoadDefaultMessages()
        {
            var englishLangKeys = new Dictionary<string, string>();

            foreach (var langEntry in LangEntry.AllLangEntries)
            {
                englishLangKeys[langEntry.Name] = langEntry.English;
            }

            lang.RegisterMessages(englishLangKeys, this, "en");
        }

        #endregion
    }
}
