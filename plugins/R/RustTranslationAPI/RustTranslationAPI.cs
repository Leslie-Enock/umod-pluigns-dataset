using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins;

[Info("Rust Translation API", "MJSU", "2.0.1")]
[Description("Provides translations for Rust entities & items")]
public class RustTranslationAPI : RustPlugin
{
    #region Class Fields

    private static readonly string LOGLine = new('=', 30);

    private readonly Dictionary<string, Dictionary<string, string>> _languages = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _constructionTokens = new();
    private readonly Dictionary<string, string> _deployableTokens = new();
    private readonly Dictionary<string, string> _displayNameTokens = new();
    private readonly Dictionary<string, string> _holdableTokens = new();
    private readonly Dictionary<string, string> _monumentTokens = new();
    private readonly Dictionary<uint, string> _prefabTokens = new();
    private bool _isInitialized;

    public enum LogLevel : byte
    {
        Off = 0,
        Error = 1,
        Warning = 2,
        Info = 3,
        Debug = 4
    }

    #endregion Class Fields

    #region Initialization

    private void OnServerInitialized()
    {
        Log($"{LOGLine}\nOnServerInitialized: start", LogLevel.Debug);
        ProcessTranslations();
        ProcessItems();
        ProcessMonuments();
        ProcessAttributes();
        _isInitialized = true;
        NextTick(() => Interface.CallHook("OnTranslationsInitialized"));
        Log("OnServerInitialized: finish", LogLevel.Debug);
    }

    private void Unload()
    {
        Log($"Plugin unloaded\n{LOGLine}\n", LogLevel.Debug);
    }

    #endregion Initialization

    #region Configuration

    private PluginConfig _pluginConfig;

    public class PluginConfig
    {
        [JsonConverter(typeof(StringEnumConverter))]
        [DefaultValue(LogLevel.Off)]
        [JsonProperty(PropertyName = "Log Level (Debug, Info, Warning, Error, Off)")]
        public LogLevel LoggingLevel { get; set; }
    }

    protected override void LoadDefaultConfig() => PrintWarning("Loading Default Config");

    protected override void LoadConfig()
    {
        base.LoadConfig();
        Config.Settings.DefaultValueHandling = DefaultValueHandling.Populate;
        _pluginConfig = AdditionalConfig(Config.ReadObject<PluginConfig>());
        Config.WriteObject(_pluginConfig);
    }

    public static PluginConfig AdditionalConfig(PluginConfig config) => config;

    #endregion Configuration

    #region Core Methods

    public void ProcessTranslations()
    {
        AssetBundleBackend assets = (AssetBundleBackend)FileSystem.Backend;
        Dictionary<string, AssetBundle> files = assets.files;

        foreach ((string path, AssetBundle bundle) in files!)
        {
            if (!path.EndsWith(".json") || !path.StartsWith("assets/localization/"))
            {
                continue;
            }

            if (bundle.LoadAsset(path) is TextAsset textAsset)
            {
                int lastIndex = path.LastIndexOf('/');
                int secondLastIndex = path.LastIndexOf('/', lastIndex - 1) + 1;
                string language = path[secondLastIndex..lastIndex];

                if (!_languages.TryGetValue(language, out Dictionary<string, string> tokens))
                {
                    _languages[language] = tokens = new Dictionary<string, string>();
                    Log($"Added language: {language}", LogLevel.Debug);
                }

                foreach ((string token, string translation) in JsonConvert.DeserializeObject<Dictionary<string, string>>(textAsset.text))
                {
                    tokens[token] = translation;
                }

                Log($"Loaded {tokens.Count} tokens for language: {language}", LogLevel.Debug);
            }
        }

        Log($"Loaded {_languages.Count} languages.\n{string.Join(", ", _languages.Keys)}", LogLevel.Debug);
    }

    public void ProcessItems()
    {
        foreach (ItemDefinition def in ItemManager.GetItemDefinitions())
        {
            _displayNameTokens[def.displayName.english] = def.displayName.token;
            BaseEntity deployableEntity = def.GetComponent<ItemModDeployable>()?.entityPrefab.GetEntity();
            if (deployableEntity)
            {
                _deployableTokens[deployableEntity.ShortPrefabName] = def.displayName.token;
                _prefabTokens[deployableEntity.prefabID] = def.displayName.token;
            }
            
            HeldEntity heldEntity = def.GetComponent<ItemModEntity>()?.entityPrefab?.Get()?.GetComponent<HeldEntity>();
            if (heldEntity&& heldEntity is not Planner && heldEntity is not Deployer)
            {
                _holdableTokens[heldEntity.ShortPrefabName] = def.displayName.token;
                _prefabTokens[heldEntity.prefabID] = def.displayName.token;
                if (heldEntity is ThrownWeapon thrownWeapon)
                {
                    BaseEntity thrownEntity = thrownWeapon.prefabToThrow.GetEntity();
                    _holdableTokens[thrownEntity.ShortPrefabName] = def.displayName.token;
                    _prefabTokens[thrownEntity.prefabID] = def.displayName.token;
                }
            }
            
            PoweredLightsDeployer poweredLights = def.GetComponent<ItemModEntity>()?.entityPrefab?.Get()?.GetComponent<PoweredLightsDeployer>();
            if (poweredLights)
            {
                _holdableTokens[poweredLights.ShortPrefabName] = def.displayName.token;
                _prefabTokens[poweredLights.prefabID] = def.displayName.token;

                BaseEntity lights = poweredLights.poweredLightsPrefab.GetEntity();
                if (lights)
                {
                    _prefabTokens[lights.prefabID] = def.displayName.token;
                }
            }
        }
    }

    public void ProcessMonuments()
    {
        foreach (MonumentInfo monumentInfo in TerrainMeta.Path.Monuments)
        {
            if (monumentInfo.displayPhrase.IsValid())
            {
                string shortPrefabName = Path.GetFileNameWithoutExtension(monumentInfo.name);
                _monumentTokens[shortPrefabName] = monumentInfo.displayPhrase.token;
            }
        }
    }

    public void ProcessAttributes()
    {
        foreach (PrefabAttribute.AttributeCollection attributes in PrefabAttribute.server.prefabs.Values)
        {
            Construction construction = attributes.Find<Construction>().FirstOrDefault();
            if (construction && !construction!.deployable && construction.info.name.IsValid())
            {
                string shortPrefabName = Path.GetFileNameWithoutExtension(construction.fullName);
                _constructionTokens[shortPrefabName] = construction.info.name.token;
                _prefabTokens[construction.prefabID] = construction.info.name.token;
            }
            
            PrefabInformation prefabInfo =  attributes.Find<PrefabInformation>().FirstOrDefault();
            if (prefabInfo)
            {
                _prefabTokens[prefabInfo!.prefabID] = prefabInfo.title.token;
            }
        }
    }

    #endregion Core Methods

    #region API Methods

    private bool IsInitialized() => _isInitialized;
    private bool IsSupportedLanguage(string language) => _languages.ContainsKey(language);
    
    private string GetTranslation(string language, string token)
    {
        if (!string.IsNullOrEmpty(language) && !string.IsNullOrEmpty(token) && _languages.TryGetValue(language, out Dictionary<string, string> tokens) && tokens.TryGetValue(token, out string translation))
        {
            return translation;
        }

        return null;
    }

    private string GetLanguage(BasePlayer player) => player?.net.connection.info.GetString("global.language", "en") ?? "en";

    private string GetTranslation(string language, Translate.Phrase token) => GetTranslation(language, token?.token);
    private string GetTranslation(BasePlayer player, Translate.Phrase token) => GetTranslation(GetLanguage(player), token?.token);
    private string GetTranslation(string language, Item item) => GetTranslation(language, item?.info);
    private string GetTranslation(BasePlayer player, Item item) => GetTranslation(GetLanguage(player), item);
    private string GetTranslation(string language, ItemDefinition def) => GetTranslation(language, def?.displayName);
    private string GetTranslation(BasePlayer player, ItemDefinition def) => GetTranslation(GetLanguage(player), def);
    
    private string GetTranslation(string language, BaseEntity entity) => GetPrefabTranslation(language, entity.prefabID);
    private string GetTranslation(BasePlayer player, BaseEntity entity) => GetPrefabTranslation(player, entity.prefabID);
    private string GetTranslation(string language, MonumentInfo monument) => GetTranslation(language, monument?.displayPhrase);
    private string GetTranslation(BasePlayer player, MonumentInfo monument) => GetTranslation(GetLanguage(player), monument);
    
    private string GetTranslation(string language, Construction construction) => GetTranslation(language, construction?.info.name.token);
    private string GetTranslation(BasePlayer player, Construction monument) => GetTranslation(GetLanguage(player), monument);
    private string GetPrefabTranslation(string language, uint prefabId) => _prefabTokens.TryGetValue(prefabId, out string token) ? GetTranslation(language, token) : null;
    private string GetPrefabTranslation(BasePlayer player, uint prefabId) => _prefabTokens.TryGetValue(prefabId, out string token) ? GetTranslation(GetLanguage(player), token) : null;
    
    private string GetItemDescriptionByID(string language, int itemID) => GetItemDescriptionByDefinition(language, ItemManager.FindItemDefinition(itemID));
    private string GetItemDescriptionByID(BasePlayer player, int itemID) => GetItemDescriptionByID(GetLanguage(player), itemID);
    private string GetItemDescriptionByDefinition(string language, ItemDefinition def) => GetTranslation(language, def?.displayDescription);
    private string GetItemDescriptionByDefinition(BasePlayer player, ItemDefinition def) => GetItemDescriptionByDefinition(GetLanguage(player), def);
    
    private string GetItemTranslationByID(string language, int itemID) => GetTranslation(language, ItemManager.FindItemDefinition(itemID));
    private string GetItemTranslationByDisplayName(string language, string displayName) => _displayNameTokens.TryGetValue(displayName, out string token) ? GetTranslation(language, token) : null;
    private string GetItemTranslationByDefinition(string language, ItemDefinition def) => GetTranslation(language, def);
    private string GetItemTranslationByShortName(string language, string itemShortName) => GetTranslation(language, ItemManager.FindItemDefinition(itemShortName));
    
    private string GetDeployableTranslation(string language, string deployable) => _deployableTokens.TryGetValue(deployable, out string token) ? GetTranslation(language, token) : null;
    private string GetHoldableTranslation(string language, string holdable) => _holdableTokens.TryGetValue(holdable, out string token) ? GetTranslation(language, token) : null;
    private string GetMonumentTranslation(string language, string monumentName) => _monumentTokens.TryGetValue(monumentName, out string token) ? GetTranslation(language, token) : null;
    private string GetMonumentTranslation(string language, MonumentInfo monumentInfo) => GetTranslation(language, monumentInfo);
    private string GetConstructionTranslation(string language, string constructionName) => _constructionTokens.TryGetValue(constructionName, out string token) ? GetTranslation(language, token) : null;
    private string GetConstructionTranslation(string language, Construction construction) => GetTranslation(language, construction);

    #endregion API Methods

    #region Helpers

    public void Log(string message, LogLevel level = LogLevel.Info, string filename = "log", [CallerMemberName] string methodName = null)
    {
        switch (level)
        {
            case LogLevel.Error:
                PrintError(message);
                message = $"{DateTime.Now:HH:mm:ss} {methodName} {message}";
                break;
            case LogLevel.Warning:
                PrintWarning(message);
                message = $"{DateTime.Now:HH:mm:ss} {methodName} {message}";
                break;
            case LogLevel.Debug:
                message = $"{DateTime.Now:HH:mm:ss} {methodName} {message}";
                break;
            case LogLevel.Off:
            case LogLevel.Info:
                break;
            default:
                message = $"{DateTime.Now:HH:mm:ss} {message}";
                break;
        }

        if (_pluginConfig.LoggingLevel >= level)
        {
            LogToFile(filename, message, this);
        }
    }

    #endregion Helpers
}