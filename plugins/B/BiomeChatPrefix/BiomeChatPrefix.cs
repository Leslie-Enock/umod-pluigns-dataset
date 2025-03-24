/*
 * TODO
 * - Add commands
 * - Add individual player exclusions
 * - Permissions for biomes instead of a all in 1 permission 
 */

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Biome Chat Prefix", "Enforcer", "2.0.2")]
    [Description("Adds a prefix when a player is in a certain biome")]
    public class BiomeChatPrefix : RustPlugin
    {
        #region References

        [PluginReference]
        Plugin BetterChat;

        #endregion

        #region Field 

        private List<JObject> groupList = new List<JObject>(); 

        #endregion

        #region Permissions

        private const string showPerm = "biomechatprefix.show";

        #endregion

        #region Init

        private void OnServerInitialized()
        {
            permission.RegisterPermission(showPerm, this);

            if (BetterChat != null && BetterChat.IsLoaded)
            {
                Unsubscribe(nameof(OnPlayerChat));
            }
        }

        #endregion

        #region Hooks

        object OnPlayerChat(BasePlayer player, string message)
        {
            if (permission.UserHasPermission(player.UserIDString, showPerm))
            {
                string region = GetRegion(player, player.transform.position);

                if (region != "NoRegion")
                {
                    Server.Broadcast(message, region, player.userID);
                    return true;
                }
            }

            return null;
        }

        Dictionary<string, object> OnBetterChat(Dictionary<string, object> data)
        {
            var player = (data["Player"] as IPlayer).Object as BasePlayer;

            if (permission.UserHasPermission(player.UserIDString, showPerm))
            {
                var message = data["Message"].ToString();

                var region = GetRegion(player, player.transform.position);
                var format = string.Format(region + " " + message);

                data["Message"] = format;
                
            }

            return data;
        }

        #endregion

        #region Functions
        
        // Gets the Region the player is currently in
        string GetRegion(BasePlayer player, Vector3 Pos)
        {
            if (config.aridRegionSettings.enableAridRegion)
            {
                if (TerrainMeta.BiomeMap.GetBiome(Pos, 1) > 0.5f)
                {
                    if (config.aridRegionSettings.useAridRegionPrefix)
                    {
                        if (player.net.connection.authLevel == 2 && config.aridRegionSettings.aridRegionExclusions.addPrefixToAdmins || player.net.connection.authLevel == 1 && config.aridRegionSettings.aridRegionExclusions.addPrefixToModerators)
                        {
                            return $"<color={config.aridRegionSettings.aridChatPrefixColour}>{config.aridRegionSettings.aridRegionPrefix}</color>";
                        }

                        return player.net.connection.authLevel >= 1 ? "NoRegion" : $"<color={config.aridRegionSettings.aridChatPrefixColour}>{config.aridRegionSettings.aridRegionPrefix}</color>";
                    }

                }
            }

            if (config.temperateRegionSettings.enableTemperateRegion)
            {
                if (TerrainMeta.BiomeMap.GetBiome(Pos, 2) > 0.5f)
                {
                    if (config.temperateRegionSettings.useTemperateRegionPrefix)
                    {
                        if (player.net.connection.authLevel == 2 && config.temperateRegionSettings.temperateRegionExclusions.addPrefixToAdmins || player.net.connection.authLevel == 1 && config.temperateRegionSettings.temperateRegionExclusions.addPrefixToModerators)
                        {
                            return $"<color={config.temperateRegionSettings.temperateChatPrefixColour}>{config.temperateRegionSettings.TemperateRegionPrefix}</color>";
                        }
                    
                        return player.net.connection.authLevel >= 1 ? "NoRegion" : $"<color={config.temperateRegionSettings.temperateChatPrefixColour}>{config.temperateRegionSettings.TemperateRegionPrefix}</color>";
                    }
                }
            }

            if (config.tundraRegionSettings.enableTundraRegion)
            {
                if (TerrainMeta.BiomeMap.GetBiome(Pos, 4) > 0.5f)
                {
                    if (config.tundraRegionSettings.useTundraRegionPrefix)
                    {
                        if (player.net.connection.authLevel == 2 && config.tundraRegionSettings.tundraRegionExclusions.addPrefixToAdmins || player.net.connection.authLevel == 1 && config.tundraRegionSettings.tundraRegionExclusions.addPrefixToModerators)
                        {
                            return $"<color={config.tundraRegionSettings.TundraChatPrefixColour}>{config.tundraRegionSettings.TundraRegionPrefix}</color>";
                        }
                    
                        return player.net.connection.authLevel >= 1 ? "NoRegion" : $"<color={config.tundraRegionSettings.TundraChatPrefixColour}>{config.tundraRegionSettings.TundraRegionPrefix}</color>";
                    }
                }
            }

            if (config.arcticRegionSettings.enableArcticRegion)
            {
                if (TerrainMeta.BiomeMap.GetBiome(Pos, 8) > 0.5f)
                {
                    if (config.arcticRegionSettings.useArcticRegionPrefix)
                    {
                        if (player.net.connection.authLevel == 2 && config.arcticRegionSettings.arcticRegionExclusions.addPrefixToAdmins || player.net.connection.authLevel == 1 && config.arcticRegionSettings.arcticRegionExclusions.addPrefixToModerators)
                        {
                            return $"<color={config.arcticRegionSettings.arcticChatPrefixColour}>{config.arcticRegionSettings.arcticRegionPrefix}</color>";
                        }
                    
                        return player.net.connection.authLevel >= 1 ? "NoRegion" : $"<color={config.arcticRegionSettings.arcticChatPrefixColour}>{config.arcticRegionSettings.arcticRegionPrefix}</color>";
                    }
                }
            }

            return "NoRegion";
        }

        #endregion

        #region Config

        ConfigData config;
        public class ConfigData
        {
            [JsonProperty(PropertyName = "Arid (Desert) Biome")]
            public AridRegionSettings aridRegionSettings { get; set; }

            [JsonProperty(PropertyName = "Temperate (Grass) Biome")]
            public TemperateRegionSettings temperateRegionSettings { get; set; }

            [JsonProperty(PropertyName = "Tundra (Forest) Biome")]
            public TundraRegionSettings tundraRegionSettings { get; set; }

            [JsonProperty(PropertyName = "Arctic (Snow) Biome")]
            public ArcticRegionSettings arcticRegionSettings { get; set; }
        }

        public class AridRegionSettings
        {
            [JsonProperty(PropertyName = "Enable arid biome")]
            public bool enableAridRegion { get; set; }

            [JsonProperty(PropertyName = "Use arid biome chat prefix")]
            public bool useAridRegionPrefix { get; set; }

            [JsonProperty(PropertyName = "Arid biome prefix")]
            public string aridRegionPrefix { get; set; }

            [JsonProperty(PropertyName = "Arid biome prefix/name colour")]
            public string aridChatPrefixColour { get; set; }

            [JsonProperty(PropertyName = "Arid biome Exclusions")]
            public Exclusions aridRegionExclusions { get; set; }
        }

        public class TemperateRegionSettings
        {
            [JsonProperty(PropertyName = "Enable temperate biome")]
            public bool enableTemperateRegion { get; set; }

            [JsonProperty(PropertyName = "Use temperate biome chat prefix")]
            public bool useTemperateRegionPrefix { get; set; }

            [JsonProperty(PropertyName = "Temperate biome prefix")]
            public string TemperateRegionPrefix { get; set; }

            [JsonProperty(PropertyName = "Temperate biome prefix/name colour")]
            public string temperateChatPrefixColour { get; set; }

            [JsonProperty(PropertyName = "Arctic Biome Exclusions")]
            public Exclusions temperateRegionExclusions { get; set; }
        }

        public class TundraRegionSettings
        {
            [JsonProperty(PropertyName = "Enable tundra biome")]
            public bool enableTundraRegion { get; set; }

            [JsonProperty(PropertyName = "Use tundra biome chat prefix")]
            public bool useTundraRegionPrefix { get; set; }

            [JsonProperty(PropertyName = "Tundra biome prefix")]
            public string TundraRegionPrefix { get; set; }

            [JsonProperty(PropertyName = "Tundra biome prefix/name colour")]
            public string TundraChatPrefixColour { get; set; }

            [JsonProperty(PropertyName = "Tundra Biome Exclusions")]
            public Exclusions tundraRegionExclusions { get; set; }
        }

        public class ArcticRegionSettings
        {
            [JsonProperty(PropertyName = "Enable arctic biome")]
            public bool enableArcticRegion { get; set; }

            [JsonProperty(PropertyName = "Use arctic biome chat prefix")]
            public bool useArcticRegionPrefix { get; set; }

            [JsonProperty(PropertyName = "Arctic biome prefix")]
            public string arcticRegionPrefix { get; set; }

            [JsonProperty(PropertyName = "Arctic biome prefix/name colour")]
            public string arcticChatPrefixColour { get; set; }

            [JsonProperty(PropertyName = "Arctic Biome Exclusions")]
            public Exclusions arcticRegionExclusions { get; set; }
        }

        public class Exclusions
        {
            [JsonProperty(PropertyName = "Add prefix to admins")]
            public bool addPrefixToAdmins { get; set; }

            [JsonProperty(PropertyName = "Add prefix to moderators")]
            public bool addPrefixToModerators { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<ConfigData>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                PrintError($"{Name}.json is corrupted! Recreating a new configuration");
                LoadDefaultConfig();
                return;
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData()
            {
                aridRegionSettings = new AridRegionSettings()
                {
                    enableAridRegion = true,
                    useAridRegionPrefix = true,
                    aridRegionPrefix = "[Desert]",
                    aridChatPrefixColour = "#D38D4B",

                    aridRegionExclusions = new Exclusions()
                    {
                        addPrefixToAdmins = true,
                        addPrefixToModerators = true,
                    }
                },

                temperateRegionSettings = new TemperateRegionSettings()
                {
                    enableTemperateRegion = true,
                    useTemperateRegionPrefix = true,
                    TemperateRegionPrefix = "[Grass]",
                    temperateChatPrefixColour = "#348C31",

                    temperateRegionExclusions = new Exclusions()
                    {
                        addPrefixToAdmins = true,
                        addPrefixToModerators = true,
                    }
                },

                tundraRegionSettings = new TundraRegionSettings()
                {
                    enableTundraRegion = true,
                    useTundraRegionPrefix = true,
                    TundraRegionPrefix = "[Forest]",
                    TundraChatPrefixColour = "#014421",

                    tundraRegionExclusions = new Exclusions()
                    {
                        addPrefixToAdmins = true,
                        addPrefixToModerators = true,
                    }
                },

                arcticRegionSettings = new ArcticRegionSettings()
                {
                    enableArcticRegion = true,
                    useArcticRegionPrefix = true,
                    arcticRegionPrefix = "[Snow]",
                    arcticChatPrefixColour = "white",

                    arcticRegionExclusions = new Exclusions()
                    {
                        addPrefixToAdmins = true,
                        addPrefixToModerators = true,
                    }
                }
            };

            PrintWarning("Creating a new configuration file!");
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion
    }
}