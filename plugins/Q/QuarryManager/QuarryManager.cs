/* 
 * TODO:
 * - Add clan support <- 2.0.4 (NEXT)
 * - Add friend support <- 2.0.4 (NEXT)
 * - Lock fuel tank, hopper/dispenser and quarry toggeling seperatly <- 2.0.5
 * - Quarry HP, repairs etc... <- 2.0.6
 * - Economics/RP support <- 2.0.6
 * - Discord support <- 2.0.7
 * - Proper help text if a player has a certain permission <- 2.0.8
 * - Quarry fuel consumption <- 2.0.8
 * - Static quarry team share support <- 2.0.9
 * - Static quarry friend share support <- 2.0.9
 * - Static quarry clan share support <- 2.0.9
 * - Static quarry added player support  <- 2.0.9
 * - More static quarry configuration options (like time until unlock after x amount of time, dome size etc.) <- 2.0.10
 * - Add chat icon <- 2.0.11
 * - Add UI <- 2.0.11
 * 
 * If you have suggestions my discord name is Enforcer#0696 or send it in the uMod help page :)
 */

using Newtonsoft.Json;
using Oxide.Core;
using Rust;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Quarry Manager", "Enforcer", "2.0.3")]
    [Description("Manage a quarry that has been placed down")]
    class QuarryManager : RustPlugin
    {
        #region Fields 

        private static QuarryManager Instance { get; set; }

        public static List<StaticQuarry> staticQuarries = new List<StaticQuarry>();
        List<SphereEntity> sphereEntities = new List<SphereEntity>();

        StringBuilder addedPlayerList = new StringBuilder();

        bool teamAccess = false;
        bool addedPlayersAccess = false;

        static bool isStaticQuarryToggled { get; set; }
        static ulong staticQuarryToggler { get; set; }
        private static bool LockZone { get; set; }

        int count = 1;

        private const int layers = Layers.Mask.Default | Layers.Mask.Water | Layers.Solid;

        static MiningQuarry _quarry;

        #endregion

        #region Permissions

        // Permission to be able to use other commands
        private const string usePerm = "quarrymanager.use";

        // Permission for team sharing
        private const string teamLockPerm = "quarrymanager.teamshare";

        // Permission for individual player sharing
        private const string playerLockPerm = "quarrymanager.playershare";

        // Permission for players to be able to bypass the static quarry dome
        private const string allowBypassPerm = "quarrymanager.allowbypass";

        // Permission for admins
        private const string adminPerm = "quarrymanager.admin";

        #endregion

        #region Init

        private void OnServerInitialized()
        {
            // Register the permissions
            permission.RegisterPermission(usePerm, this);
            permission.RegisterPermission(teamLockPerm, this);
            permission.RegisterPermission(playerLockPerm, this);
            permission.RegisterPermission(allowBypassPerm, this);
            permission.RegisterPermission(adminPerm, this);

            // Add the command from the config
            cmd.AddChatCommand(config.commands.qmCommand, this, nameof(QuarryShareCommand));
        }

        private void Loaded()
        {
            // Load the data file
            LoadData();

            // Checks if static quarry is enabled before searching for the prefab (Searching through all entities is bad I will change that in the next update)
            if (config.shareSettings.staticQuarrySharing.enableStaticQuarryShare)
            {
                foreach (var entity in BaseNetworkable.serverEntities)
                {
                    FindStaticQuarry(entity);
                }
            }

            playerData = Interface.Oxide.DataFileSystem.ReadObject<PlayerData>(Name);

            Instance = this;

            LockZone = false;
            isStaticQuarryToggled = false;
        }

        void Unload()
        {
            // Destory all spheres and checks if static quarries are enabled (Again searching through alot of entities is bad I will change it in the next update)
            if (config.shareSettings.staticQuarrySharing.enableStaticQuarryShare)
            {
                foreach (var spheres in BaseNetworkable.serverEntities)
                {
                    var sphereE = spheres as SphereEntity;
                    if (sphereEntities.Contains(sphereE))
                    {
                        sphereE.Kill();
                    }
                }

                // Kills/destroys all existing static quarries
                foreach (var quarry in staticQuarries)
                {
                    if (staticQuarries.Contains(quarry))
                    {
                        StaticQuarry.Destroy(quarry);
                    }
                }
            }

            config = null;
        }

        void OnNewSave()
        {
            if (config.generalSettings.wipeDataOnNewSave)
            {
                WipeData();
            }
        }

        #endregion

        #region Config

        ConfigData config;
        private class ConfigData
        {
            [JsonProperty("General Settings")]
            public GeneralSettings generalSettings { get; set; }

            [JsonProperty("Commands")]
            public Commands commands { get; set; }

            [JsonProperty("Chat Settings")]
            public ChatSettings chatSettings { get; set; }

            [JsonProperty("Share Settings")]
            public ShareSettings shareSettings { get; set; }
        }

        public class GeneralSettings
        {
            [JsonProperty(PropertyName = "Wipe Data on new save")]
            public bool wipeDataOnNewSave { get; set; }
        }

        public class Commands
        {
            [JsonProperty(PropertyName = "Quarry Manager Command")]
            public string qmCommand { get; set; }
        }

        public class ChatSettings
        {
            [JsonProperty(PropertyName = "Chat prefix")]
            public string chatPrefix { get; set; }

            [JsonProperty(PropertyName = "Chat prefix colour")]
            public string chatPrefixColour { get; set; }

            [JsonProperty(PropertyName = "Chat icon")]
            public ulong chatIcon { get; set; }

            [JsonProperty("Quarry Share Messages")]
            public QuarryShareMessages quarryShareMessages { get; set; }

            [JsonProperty("RCON/Server Console Messages")]
            public ServerConsoleMessages serverConsoleMessages { get; set; }
        }

        public class QuarryShareMessages
        {
            [JsonProperty(PropertyName = "Print a message if team share is enabled")]
            public bool messageOnTeamShareEnabled { get; set; }

            [JsonProperty(PropertyName = "Print a message if team share is disabled")]
            public bool messageOnTeamShareDisabled { get; set; }

            [JsonProperty(PropertyName = "Print a message if a player is added to the share list")]
            public bool messageOnAddedToPlayerList { get; set; }

            [JsonProperty(PropertyName = "Print a message if a player is removed to the share list")]
            public bool messageOnRemovedFromPlayerList { get; set; }

            [JsonProperty(PropertyName = "Print a message if a player has activated a static quarry")]
            public bool messageOnStaticQuarryBeingUsed { get; set; }
        }

        public class ServerConsoleMessages
        {
            [JsonProperty(PropertyName = "Message the console if someone enables team share")]
            public bool messageRCONOnEnabledTeamShare { get; set; }

            [JsonProperty(PropertyName = "Message the console if someone disables team share")]
            public bool messageRCONOnDisableTeamShare { get; set; }

            [JsonProperty(PropertyName = "Message the console if someone is added to the share list")]
            public bool messageRCONOnAddedToShareList { get; set; }

            [JsonProperty(PropertyName = "Message the console if a player is removed to the share list")]
            public bool messageRCONOnRemovedToPlayerList { get; set; }

            [JsonProperty(PropertyName = "Message the console on static quarry lock activated")]
            public bool messageRCONOnStaticQuarryBeingUsed { get; set; }
        }

        public class ShareSettings
        {
            [JsonProperty(PropertyName = "Players can toggle other players quarries")]
            public bool canToggleQuarry { get; set; }

            [JsonProperty(PropertyName = "Lock quarry containers")]
            public bool canOpenQuarryContainers { get; set; }

            [JsonProperty("Team Sharing")]
            public TeamSharing teamSharing { get; set; }

            [JsonProperty("Individual Player Sharing")]
            public IndividualPlayerSharing individualPlayerSharing { get; set; }

            [JsonProperty("Static Quarry Sharing")]
            public StaticQuarrySharing staticQuarrySharing { get; set; }
        }

        public class TeamSharing
        {
            [JsonProperty(PropertyName = "Allow team to toggle the quarry")]
            public bool canTeamToggleQuarry { get; set; }

            [JsonProperty(PropertyName = "Allow team to open the quarries containers")]
            public bool canTeamOpenQuarry { get; set; }
        }

        public class IndividualPlayerSharing
        {
            [JsonProperty(PropertyName = "Allow added players to toggle the quarry")]
            public bool canAddedPlayersToggleQuarry { get; set; }

            [JsonProperty(PropertyName = "Allow added players to open the quarries containers")]
            public bool canAddedPlayersOpenQuarry { get; set; }

        }

        public class StaticQuarrySharing
        {
            [JsonProperty(PropertyName = "Enable static quarry lock")]
            public bool enableStaticQuarryShare { get; set; }

            [JsonProperty("Static Quarry Sharing")]
            public LocalSharing localSharing { get; set; }
        }

        public class LocalSharing
        {
            [JsonProperty(PropertyName = "If the player leaves the dome unlock the static quarry")]
            public bool unlockStaticQuarryOnExitDome { get; set; }
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
                for (var i = 0; i < 3; i++) PrintError($"{Name}.json is corrupted! Recreating a new configuration");
                LoadDefaultConfig();
                return;
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData()
            {
                generalSettings = new GeneralSettings()
                {
                    wipeDataOnNewSave = true
                },

                commands = new Commands()
                {
                    qmCommand = "qm"
                },

                chatSettings = new ChatSettings()
                {
                    chatPrefix = "QuarryLocker >>",
                    chatPrefixColour = "#FF6A13",
                    chatIcon = 0,

                    quarryShareMessages = new QuarryShareMessages()
                    {
                        messageOnTeamShareEnabled = true,
                        messageOnTeamShareDisabled = true,
                        messageOnAddedToPlayerList = true,
                        messageOnRemovedFromPlayerList = true,
                        messageOnStaticQuarryBeingUsed = true
                    },

                    serverConsoleMessages = new ServerConsoleMessages()
                    {
                        messageRCONOnEnabledTeamShare = false,
                        messageRCONOnDisableTeamShare = false,
                        messageRCONOnAddedToShareList = false,
                        messageRCONOnRemovedToPlayerList = false,
                        messageRCONOnStaticQuarryBeingUsed = false
                    }
                },

                shareSettings = new ShareSettings()
                {
                    canToggleQuarry = false,
                    canOpenQuarryContainers = false,
                    teamSharing = new TeamSharing()
                    {
                        canTeamToggleQuarry = true,
                        canTeamOpenQuarry = true
                    },
                    individualPlayerSharing = new IndividualPlayerSharing()
                    {
                        canAddedPlayersToggleQuarry = true,
                        canAddedPlayersOpenQuarry = true
                    },

                    staticQuarrySharing = new StaticQuarrySharing()
                    {
                        enableStaticQuarryShare = false,

                        localSharing = new LocalSharing()
                        {
                            unlockStaticQuarryOnExitDome = false
                        }
                    }
                }
            };

            PrintError("Creating a new configuration file!");
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Data 


        private PlayerData playerData;
        public class PlayerData
        {
            public Dictionary<ulong, User> players = new Dictionary<ulong, User>();

            public User AddPlayer(ulong playerID, bool isTeamShareEnabled, Dictionary<ulong, string> addedPlayers)
            {
                User player = new User();
                players.Add(playerID, player);
                isTeamShareEnabled = player.EnabledTeamShare;
                addedPlayers = player.addedPlayers;
                return player;
            }
            public bool GetPlayer(ulong playerID)
            {
                if (players.ContainsKey(playerID))
                {
                    return true;
                }

                return false;
            }
        }

        public class User
        {
            public bool EnabledTeamShare;
            public Dictionary<ulong, string> addedPlayers = new Dictionary<ulong, string>();

        }

        void LoadData()
        {
            // If the data does not exist then just make a new one
            try
            {
                playerData = Interface.Oxide.DataFileSystem.ReadObject<PlayerData>(Name);
            }
            catch
            {
                Puts("QuarryManager.json was not found! Creating a new one.");
                playerData = new PlayerData();
            }
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, playerData);
        }

        private void WipeData()
        {
            if (playerData == null)
                return;
            
                PrintWarning("[QuarryManager]: QuarryManager.json has been wiped!");
                playerData.players.Clear();
            
        }

        #endregion

        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
                {
                    {"Info.Help", $"\n /{config.commands.qmCommand} h | help - Provides the following commands: \n /{config.commands.qmCommand} t | team - Enable/Disable quarry team share. \n /{config.commands.qmCommand} a | add - Adds a certain player that can access your quarry \n /{config.commands.qmCommand} r | remove - Removes a certain added player that can access your quarry \n /{config.commands.qmCommand} ra | removeall - Removes all added players that can access your quarry \n /{config.commands.qmCommand} l | list - Lists all added players that can access your quarry" },
                    {"Info.TeamSharingEnabled", "Your team can now access your quarry" },
                    {"Info.TeamSharingDisabled", "Your team can no longer access your quarry" },
                    {"Info.AddedPlayer", "{0} Can now access your quarries" },
                    {"Info.RemovedPlayer", "{0} Can no longer access your quarries" },
                    {"Info.RemovedAllPlayers", "No one can access your quarries that you have added" },
                    {"Info.NoAddedPlayers", "There are no players to list since you have not added any yet" },
                    {"Info.ListCurrentPlayers", "Players that are added that can access your quarries:\n{0}" },
                    {"Info.StaticQuarryActivated", "No one can access this quarry unless you leave the dome or the quarries fuel runs out" },
                    {"Info.MessageRconOnTeamShareEnabled", "{0}, enabled team share!"},
                    {"Info.MessageRconOnTeamShareDisabled", "{0}, disabled team share!"},
                    {"Info.MessageRconOnPlayerAdded", "{0}, added {1}"},
                    {"Info.MessageRconOnPlayerRemoved", "{0}, removed {1}"},
                    {"Info.MessageRconOnStaticQuarryActivated", "The static quarry has been activated at: {0} by {1}" },
                    {"Warning.NoPermission", "You don't have the required permissions to use this command" },
                    {"Warning.UnableToFindPlayer", "Unable to find the player!" },
                    {"Warning.UnableToAddSelf", "You cannot add yourself" },
                    {"Warning.UnableToAccessContainer", "You cannot access this quarry, since you are not authorized on it" },
                    {"Warning.UnableToToggleEngine", "You are unable able to toggle {0}'s quarry" },
                    {"Warning.NoTeamFound", "You cannot use this command since you don't have a team" },
                }, this, "en");
        }

        #endregion

        #region Helpers

        // Checks if player is an admin using a permission
        private bool IsAdmin(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, adminPerm))
                return true;

            return false;
        }

        private void Message(string message, BasePlayer player, params string[] args) => SendReply(player, lang.GetMessage($"<color={config.chatSettings.chatPrefixColour}>{config.chatSettings.chatPrefix}</color> {message}", this), args);

        private void MessageConsole(string message, params string[] args) => PrintWarning(lang.GetMessage($"{config.chatSettings.chatPrefix} {message}", this), args);

        #endregion

        #region Commands

        // The command that is listed in the config
        void QuarryShareCommand(BasePlayer bPlayer, string command, params string[] args)
        {
            // If the player just types /qm or the length is 0 meaning if it is not /qm add or something like that then send them a help message
            if (args == null || args.Length == 0)
            {
                // Checks if has permission
                if (!permission.UserHasPermission(bPlayer.UserIDString, usePerm))
                {
                    Message(lang.GetMessage("Warning.NoPermission", this, bPlayer.UserIDString), bPlayer);
                    return;
                }

                Message(lang.GetMessage("Info.Help", this, bPlayer.UserIDString), bPlayer);
                return;
            }

            // We don't want the player trying to toggle team share without a team (if you change the team sub command then make sure you change this)
            if (args.Contains("team") || args.Contains("t"))
            {
                // Checks if has permission
                if (!permission.UserHasPermission(bPlayer.UserIDString, teamLockPerm))
                {
                    Message(lang.GetMessage("Warning.NoPermission", this, bPlayer.UserIDString), bPlayer);
                    return;
                }

                // Checks they don't have a team
                if (bPlayer.currentTeam == 0)
                {
                    Message(lang.GetMessage("Warning.NoTeamFound", this, bPlayer.UserIDString), bPlayer);
                    return;
                }
            }

            switch (args[0].ToLower())
            {
                case "h":
                case "help":

                    // Checks if has permission
                    if (!permission.UserHasPermission(bPlayer.UserIDString, usePerm))
                    {
                        Message(lang.GetMessage("Warning.NoPermission", this, bPlayer.UserIDString), bPlayer);
                        return;
                    }

                    Message(lang.GetMessage("Info.Help", this, bPlayer.UserIDString), bPlayer);

                    return;

                // If you change the team commands (t or team) make sure you change the ones on line 541 as well if you don't then it will not work as intended
                case "t":
                case "team":

                    // Checks if has permission
                    if (!permission.UserHasPermission(bPlayer.UserIDString, teamLockPerm))
                    {
                        Message(lang.GetMessage("Warning.NoPermission", this, bPlayer.UserIDString), bPlayer);
                        return;
                    }

                    // Checks if the player is not in the data file
                    if (!playerData.GetPlayer(bPlayer.userID))
                    {
                        playerData.AddPlayer(bPlayer.userID, false, null);
                        SaveData();
                    }

                    // Checks if the player has enabled team share
                    if (playerData.players[bPlayer.userID].EnabledTeamShare)
                    {
                        if (config.chatSettings.quarryShareMessages.messageOnTeamShareEnabled)
                        {
                            Message(lang.GetMessage("Info.TeamSharingDisabled", this, bPlayer.UserIDString), bPlayer);
                        }

                        if (config.chatSettings.serverConsoleMessages.messageRCONOnEnabledTeamShare)
                        {
                            MessageConsole(lang.GetMessage("Info.MessageRconOnTeamShareDisabled", this, bPlayer.UserIDString), bPlayer.displayName);

                        }

                        playerData.players[bPlayer.userID].EnabledTeamShare = false;
                        SaveData();
                    }
                    else
                    {
                        // Now checks if the player has disabled team share
                        if (config.chatSettings.quarryShareMessages.messageOnTeamShareDisabled)
                        {
                            Message(lang.GetMessage("Info.TeamSharingEnabled", this, bPlayer.UserIDString), bPlayer);
                        }

                        if (config.chatSettings.serverConsoleMessages.messageRCONOnDisableTeamShare)
                        {
                            MessageConsole(lang.GetMessage("Info.MessageRconOnTeamShareEnabled", this, bPlayer.UserIDString), bPlayer.displayName);

                        }

                        playerData.players[bPlayer.userID].EnabledTeamShare = true;
                        SaveData();
                    }

                    return;

                case "a":
                case "add":

                    // Checks if has permission
                    if (!permission.UserHasPermission(bPlayer.UserIDString, playerLockPerm))
                    {
                        Message(lang.GetMessage("Warning.NoPermission", this, bPlayer.UserIDString), bPlayer);
                        return;
                    }

                    // Checks if the player is not in the data file
                    if (!playerData.GetPlayer(bPlayer.userID))
                    {
                        playerData.AddPlayer(bPlayer.userID, false, null);
                        SaveData();
                    }

                    // Checks if the player is trying to add 2 players. Eg: /qm add enforcer player1 <- Arg 2 (I will probably add the ability to add multiple players)
                    if (args.Length != 2)
                    {
                        Message(lang.GetMessage("Warning.UnableToFindPlayer", this, bPlayer.UserIDString), bPlayer);
                        return;
                    }

                    AddOrRemoveUser(bPlayer, true, false, args);

                    return;

                case "r":
                case "remove":

                    // Checks if has permission
                    if (!permission.UserHasPermission(bPlayer.UserIDString, playerLockPerm))
                    {
                        Message(lang.GetMessage("Warning.NoPermission", this, bPlayer.UserIDString), bPlayer);
                        return;
                    }

                    // Checks if the player is not in the data file
                    if (!playerData.GetPlayer(bPlayer.userID))
                    {
                        playerData.AddPlayer(bPlayer.userID, false, null);
                        SaveData();
                    }

                    // Checks if the player is trying to remove 2 players. Eg: /qm remove enforcer player1 <- Arg 2 (I will probably add the ability to remove multiple players)
                    if (args.Length != 2)
                    {
                        Message(lang.GetMessage("Warning.UnableToFindPlayer", this, bPlayer.UserIDString), bPlayer);
                        return;
                    }

                    AddOrRemoveUser(bPlayer, false, true, args);

                    return;

                case "ra":
                case "removeall":

                    // Checks if has permission
                    if (!permission.UserHasPermission(bPlayer.UserIDString, playerLockPerm))
                    {
                        Message(lang.GetMessage("Warning.NoPermission", this, bPlayer.UserIDString), bPlayer);
                        return;
                    }

                    // Checks if the player is not in the data file
                    if (!playerData.GetPlayer(bPlayer.userID))
                    {
                        playerData.AddPlayer(bPlayer.userID, false, null);
                        SaveData();
                    }

                    // If the player added no one then just return it
                    if (playerData.players[bPlayer.userID].addedPlayers.Count == 0)
                        return;
                    
                    // Clears the concurrent players
                    playerData.players[bPlayer.userID].addedPlayers.Clear();
                    addedPlayersAccess = false;

                    Message(lang.GetMessage("Info.RemovedAllPlayers", this, bPlayer.UserIDString), bPlayer);

                    SaveData();

                    return;

                case "l":
                case "list":

                    // Checks if has permission
                    if (!permission.UserHasPermission(bPlayer.UserIDString, playerLockPerm))
                    {
                        Message(lang.GetMessage("Warning.NoPermission", this, bPlayer.UserIDString), bPlayer);
                        return;
                    }

                    // Checks if the player is not in the data file
                    if (!playerData.GetPlayer(bPlayer.userID))
                    {
                        playerData.AddPlayer(bPlayer.userID, false, null);
                        SaveData();
                    }

                    // Again checking if the player has no one added and prints a message and returns
                    if (playerData.players[bPlayer.userID].addedPlayers.Count == 0)
                    {
                        Message(lang.GetMessage("Info.NoAddedPlayers", this, bPlayer.UserIDString), bPlayer);
                        return;
                    }

                    // Gets all added players and print it out. Count by index and if that player meets that index value then print their name
                    foreach (var addedPlayer in playerData.players[bPlayer.userID].addedPlayers)
                    {
                        addedPlayerList.Append($"{count}. {addedPlayer.Value}\n");
                        count++;
                    }

                    Message(lang.GetMessage("Info.ListCurrentPlayers", this, bPlayer.UserIDString), bPlayer, addedPlayerList.ToString());

                    return;

                    // This area is to check if the player did something like /qm test. Since the sub command test does not exist then just send them to this area 
                default:

                    // Checks if the player has the required permission
                    if (!permission.UserHasPermission(bPlayer.UserIDString, usePerm))
                    {
                        Message(lang.GetMessage("Warning.NoPermission", this, bPlayer.UserIDString), bPlayer);
                        return;
                    }

                    Message(lang.GetMessage("Info.Help", this, bPlayer.UserIDString), bPlayer);

                    return;
            }
        }

        #endregion

        #region Hooks 

        // Oxide hook to check if the player is able to loot the container of the quarry 
        object CanLootEntity(BasePlayer bPlayer, ResourceExtractorFuelStorage container)
        {
            // Checks if the quarry container id is equalled to 0 or the player is an admin
            if (container.OwnerID == 0 || IsAdmin(bPlayer))
                return null;

            if (container.OwnerID != bPlayer.userID)
            {
                if (!config.shareSettings.canOpenQuarryContainers)
                    return null;

                if(playerData.players.ContainsKey(container.OwnerID))
                {
                    teamAccess = playerData.players[container.OwnerID].EnabledTeamShare;

                    if (teamAccess)
                    {
                        if (config.shareSettings.teamSharing.canTeamOpenQuarry)
                        {
                            if (BasePlayer.FindAwakeOrSleeping(container.OwnerID.ToString()).currentTeam != bPlayer.currentTeam)
                                return true;

                            // Returning true does not allow them to loot the containers. Returning null allows them to loot the containers
                            if (playerData.players[container.OwnerID].EnabledTeamShare)
                            {
                                return null;
                            }
                            else
                            {
                                Message(lang.GetMessage("Warning.UnableToAccessContainer", this, bPlayer.UserIDString), bPlayer/*, config.chatsettings.chaticon*/);
                                return true;
                            }

                        }
                    }

                    if (!addedPlayersAccess)
                    {
                        if (playerData.players[container.OwnerID].addedPlayers.Count == 0)
                        {
                            addedPlayersAccess = false;
                        }
                        else
                        {
                            addedPlayersAccess = true;
                        }
                    }

                    if (addedPlayersAccess)
                    {
                        if (config.shareSettings.individualPlayerSharing.canAddedPlayersOpenQuarry)
                        {
                            if (playerData.players[container.OwnerID].addedPlayers.ContainsKey(bPlayer.userID))
                            {
                                return null;
                            }
                            else
                            {
                                Message(lang.GetMessage("Warning.UnableToAccessContainer", this, bPlayer.UserIDString), bPlayer/*, config.chatsettings.chaticon*/);
                                return true;
                            }
                        }
                    }
                }

                Message(lang.GetMessage("Warning.UnableToAccessContainer", this, bPlayer.UserIDString), bPlayer/*, config.chatsettings.chaticon*/);
                return true;
            }

            return null;
        }

        private void OnQuarryToggled(MiningQuarry quarry, BasePlayer bPlayer)
        {
            // Static quarry part

            // Checks if the quarry is static
            if (quarry.isStatic)
            {
                if (!LockZone)
                {
                    if (quarry.IsOn())
                    {
                        // Locks the zone using an ejector component (StaticQuarry) class
                        isStaticQuarryToggled = true;
                        staticQuarryToggler = bPlayer.userID;

                        LockZone = true;

                        if (config.chatSettings.quarryShareMessages.messageOnStaticQuarryBeingUsed)
                        {
                            Message(lang.GetMessage("Info.StaticQuarryActivated", this, bPlayer.UserIDString), bPlayer/*, config.chatsettings.chaticon*/);
                        }

                        if (config.chatSettings.serverConsoleMessages.messageRCONOnStaticQuarryBeingUsed)
                        {
                            MessageConsole(lang.GetMessage("Info.MessageRconOnStaticQuarryActivated", this, bPlayer.UserIDString), quarry.transform.position.ToString(), bPlayer.displayName);
                        }

                        return;
                    }
                }
            }
            else
            {
                // Normal quarry part

                if (config.shareSettings.canToggleQuarry)
                    return;

                if (quarry == null)
                    return;

                if (quarry.OwnerID == 0 || quarry.OwnerID == bPlayer.userID || IsAdmin(bPlayer))
                    return;

                // Had to do this part since people were able to loot the quarry some how for some reason. This just gets the item and checks if it inside the quarry
                var item = quarry.fuelStoragePrefab.instance.GetComponent<StorageContainer>().inventory.FindItemsByItemName("lowgradefuel");

                if (item == null)
                {
                    Message(lang.GetMessage("Warning.UnableToAccessContainer", this, bPlayer.UserIDString), bPlayer/*, config.chatsettings.chaticon*/);
                    return;
                }

                teamAccess = playerData.players[quarry.OwnerID].EnabledTeamShare;

                if (teamAccess)
                {
                    if (config.shareSettings.teamSharing.canTeamToggleQuarry)
                    {
                        // Checks if the player is inside the quarry owners team
                        if (BasePlayer.FindAwakeOrSleeping(quarry.OwnerID.ToString()).currentTeam == bPlayer.currentTeam)
                        {
                            if (playerData.players[quarry.OwnerID].EnabledTeamShare)
                                return;
                            else
                            {
                                quarry.SetOn(!quarry.IsOn());
                                Message(lang.GetMessage("Warning.UnableToAccessContainer", this, bPlayer.UserIDString), bPlayer/*, config.chatsettings.chaticon*/);
                            }
                        }
                    }
                }

                if (!addedPlayersAccess)
                {
                    if (playerData.players[quarry.OwnerID].addedPlayers.Count == 0)
                    {
                        addedPlayersAccess = false;
                    }
                    else
                    {
                        addedPlayersAccess = true;
                    }
                }

                if (addedPlayersAccess)
                {
                    if (config.shareSettings.individualPlayerSharing.canAddedPlayersToggleQuarry)
                    {
                        if (!playerData.players[quarry.OwnerID].addedPlayers.ContainsKey(bPlayer.userID))
                        {
                            quarry.SetOn(!quarry.IsOn());
                            Message(lang.GetMessage("Warning.UnableToAccessContainer", this, bPlayer.UserIDString), bPlayer/*, config.chatsettings.chaticon*/);
                        }
                        else
                        {
                            return;
                        }
                    }
                }

                quarry.SetOn(!quarry.IsOn());

                // There was a bug that could drain peoples fuel when toggled and so I just plus it so it does not happen. This is for someone who tries to access it without the correct conditions met
                item.amount++;
                Message(lang.GetMessage("Warning.UnableToAccessContainer", this, bPlayer.UserIDString), bPlayer/*, config.chatsettings.chaticon*/);
            }
        }

        #endregion

        #region Functions  

        // I will make this better and allow non caps. It is currently case sensitive
        public BasePlayer FindPlayer(string nameOrID)
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (player.userID.ToString() == nameOrID)
                    return player;

                if (player.displayName == nameOrID)
                    return player;

                if (player.name.Contains(nameOrID))
                    return player;
            }

            return null;
        }

        // Adds or remove user
        private void AddOrRemoveUser(BasePlayer player, bool isAdding, bool isRemoving, string[] args)
        {
            var user = FindPlayer(args[1]);

            if (isAdding)
            {
                if (user.userID == player.userID)
                {
                    Message(lang.GetMessage("Warning.UnableToAddSelf", this, player.UserIDString), player);
                    return;
                }

                if (user == null)
                {
                    Message(lang.GetMessage("Warning.UnableToFindPlayer", this, player.UserIDString), player, user.displayName);
                    return;
                }

                if (playerData.players[player.userID].addedPlayers.ContainsKey(user.userID))
                {
                    Message(lang.GetMessage("Warning.AlreadyAdded", this, player.UserIDString), player, user.displayName);
                    return;
                }

                playerData.players[player.userID].addedPlayers.Add(user.userID, user.displayName);
                SaveData();

                if (config.chatSettings.quarryShareMessages.messageOnAddedToPlayerList)
                {
                    Message(lang.GetMessage("Info.AddedPlayer", this, player.UserIDString), player, user.displayName);
                }

                if (config.chatSettings.serverConsoleMessages.messageRCONOnAddedToShareList)
                {
                    MessageConsole(lang.GetMessage("Info.MessageRconOnPlayerAdded", this, player.UserIDString), player.displayName, user.displayName);
                }

                return;
            }

            if (isRemoving)
            {
                if (playerData.players[player.userID].addedPlayers.Count == 0)
                    return;

                if (!playerData.players[player.userID].addedPlayers.ContainsKey(user.userID))
                    return;

                playerData.players[player.userID].addedPlayers.Remove(user.userID);
                SaveData();

                if (config.chatSettings.quarryShareMessages.messageOnRemovedFromPlayerList)
                {
                    Message(lang.GetMessage("Info.RemovedPlayer", this, player.UserIDString), player, user.displayName);
                }

                if (config.chatSettings.serverConsoleMessages.messageRCONOnRemovedToPlayerList)
                {
                    MessageConsole(lang.GetMessage("Info.MessageRconOnPlayerRemoved", this, player.UserIDString), player.displayName, user.displayName);
                }

                return;
            }
        }

        // Random thing I made to check if the user is inside added players
        //private bool IsUserValid(BasePlayer player, string[] args)
        //{
        //    var user = FindPlayer(args[1]);

        //    if (playerData.players[player.userID].addedPlayers.ContainsKey(user.userID))
        //    {
        //        return true;
        //    }

        //    return false;
        //}

        // Gets the entity
        private static BaseEntity GetEntity(Collider collider)
        {
            var entity = collider.ToBaseEntity();

            while (entity != null && entity.HasParent() && !(entity is BaseMountable) && !(entity is BasePlayer))
            {
                entity = entity.GetParentEntity();
            }

            return entity;
        }

        // Credits to ZoneManager for some of this part
        public static Vector3 MoveLocation(Vector3 position, float distance, Vector3 target, float radius, float yAmount)
        {
            var location = ((position.XZ3D() - target.XZ3D()).normalized * (distance + radius)) + target;

            var y = TerrainMeta.HighestPoint.y + 300f;

            RaycastHit raycast;
            if (Physics.Raycast(new Ray(new Vector3(0f, y, 0f), Vector3.down), out raycast, 500, layers, QueryTriggerInteraction.Ignore))
            {
                location.y = raycast.point.y + yAmount;
            }
            else location.y = TerrainMeta.HeightMap.GetHeight(location) + yAmount;

            return location;
        }

        // Moves the player
        public static bool MovePlayer(BasePlayer bPlayer, Vector3 pos)
        {
            // Checks if the player is sleeping
            if (bPlayer.IsSleeping())
            {
                return false;
            }

            // Gets the move position
            var movePos = MoveLocation(bPlayer.transform.position, 5f, pos, 30f, 1f);

            // Checks if the player is flying
            if (bPlayer.IsFlying)
            {
                pos.y = bPlayer.transform.position.y;
            }

            // Teleports the player
            bPlayer.Teleport(movePos);
            bPlayer.SendNetworkUpdateImmediate();

            return true;
        }

        // Moves the mountable 
        public static bool MoveMountable(BaseMountable bMountable, Vector3 pos, float distance, float radius)
        {
            BaseVehicle vehicle = bMountable as BaseVehicle;
            var movePos = MoveLocation(bMountable.transform.position, distance, pos, radius, 10f);

            // Checks if vehicle is null or destoryed 
            if (vehicle == null || vehicle.IsDestroyed)
                return false;

            bMountable.transform.position = movePos;

            return true;
        }

        // A check to see if the static quarry contains fuel
        private static bool IsStaticQuarryContainsFuel(MiningQuarry quarry)
        {
            var fuelItem = quarry.fuelStoragePrefab.instance.GetComponent<StorageContainer>().inventory.FindItemsByItemName("lowgradefuel");

            if (quarry.isStatic)
            {
                if (fuelItem == null)
                    return false;

                return true;
            }

            return false;
        }


        #endregion

        #region Static Quarry

        // Finds the quarry
        void FindStaticQuarry(BaseNetworkable entity)
        {
            // Checks if entity is null
            if (entity == null)
                return;

            // Gets the component since BaseNetworkable does not have it 
            var quarry = entity.GetComponent<MiningQuarry>();

            // Checks if the quarry is a quarry. Probably should just do if(quarry) instead of that.
            if (quarry is MiningQuarry)
            {
                // Checks if the quarry is static
                if (quarry.isStatic)
                {
                    // Create a sphere. Will add an option in cofig to change how big it should be or if it should be enabled/disabled.
                    CreateSphere(entity.transform.position, 60f);

                    // Makes the collider. The reason it is 30 meters is because for some reason the collider was way out of where it should be (it was too big).
                    StaticQuarry.Init(entity.transform.position, 30f);
                }
            }
        }

        void CreateSphere(Vector3 spherePosition, float radius)
        {
            // Create sphere
            var sphere = GameManager.server.CreateEntity("assets/prefabs/visualization/sphere.prefab", spherePosition, Quaternion.identity) as SphereEntity;

            // Check if it is null 
            if (sphere == null)
                return;

            var sphereEntity = sphere.GetComponent<SphereEntity>();

            sphereEntity.currentRadius = radius;
            sphereEntity.lerpSpeed = 0f;

            // Spawn it
            sphere.Spawn();

            // Add it to the list
            sphereEntities.Add(sphere);
        }

        // Ejector component
        public class StaticQuarry : MonoBehaviour
        {
            private static StaticQuarry staticQuarry;
            private GameObject _gameObject;

            private float colRadius;

            // When creating a sphere and expecting something to happen it is not the actual sphere is it a collider. Below is how we make it

            // Credits to RaidableBases for Init()
            public static void Init(Vector3 position, float radius)
            {
                GameObject gameObject;

                if (staticQuarry == null)
                {
                    gameObject = new GameObject();

                    staticQuarry = gameObject.AddComponent<StaticQuarry>();
                    staticQuarry._gameObject = gameObject;
                }
                else
                {
                    gameObject = staticQuarry._gameObject;
                }

                // Sets the position
                gameObject.transform.position = staticQuarry._gameObject.transform.position = position;
                gameObject.layer = (int) Layer.Trigger;

                // Add a sphere colider to it 
                var col = gameObject.AddComponent<SphereCollider>();

                // Make sure the center of the collider is zero and is in the actual center
                col.center = Vector3.zero;

                // Set the radius 
                col.radius = staticQuarry.colRadius = radius;

                // Make it a trigger
                col.isTrigger = true;

                // Add the component to the static quarries list
                staticQuarries.Add(staticQuarry);
            }

            // Called when a player enters the collider
            private void OnTriggerEnter(Collider collider)
            {
                if (collider == null)
                    return;

                // Gets the collider
                var entity = GetEntity(collider);

                // Checks if the entity is a player 
                if (entity is BasePlayer)
                {
                    var bP = entity as BasePlayer;

                    if (IsBypassAllowed(bP))
                    {
                        return;
                    }

                    if (isStaticQuarryToggled)
                    {
                        // If the quarry toggler does not equal the player who is trying to enter steam ID then move the player but first check if the zone is locked 
                        if (staticQuarryToggler != bP.userID)
                        {
                            if (LockZone)
                            {
                                MovePlayer(bP, staticQuarry.gameObject.transform.position);

                                // Needed because sometimes the actual thing that we are trying to do does not happen on client
                                bP.SendNetworkUpdateImmediate();
                            }
                        }
                        else
                        {
                            return;
                        }
                    }
                }

                // Check if entity is a mountable
                if (entity is BaseMountable)
                {
                    var bM = entity as BaseMountable;

                    // Get the mounted player
                    var bPlayer = bM.GetMounted();
                    var mountedPlayer = new List<BasePlayer>();

                    // Adds the monuted player to the list above 
                    mountedPlayer.Add(bPlayer);

                    // Checks if the player is in the list
                    if (mountedPlayer.Contains(bPlayer))
                    {
                        // Check if the zone is locked meaning able to enter and wont push the player back
                        if (LockZone)
                        {
                            // Move and send an update 
                            MoveMountable(bM, staticQuarry.gameObject.transform.position, 5f, 30f);
                            bM.SendNetworkUpdateImmediate();
                        }
                    }
                    else
                    {
                        return;
                    }
                }
            }

            // Called when a player leaves the collider
            private void OnTriggerExit(Collider collider)
            {
                if (collider == null)
                    return;

                // Get the collider in this case static quarry
                var entity = GetEntity(collider);

                // Checks if the entity is a player. This part is pretty straight forward.
                if (entity is BasePlayer)
                {
                    var bP = entity as BasePlayer;

                    if (Instance.config.shareSettings.staticQuarrySharing.localSharing.unlockStaticQuarryOnExitDome)
                    {
                        if (staticQuarryToggler == bP.userID)
                        {
                            LockZone = false;
                            staticQuarryToggler = 0;
                        }
                        else
                        {
                            return;
                        }

                    }
                }

                if (entity is BaseMountable)
                {
                    var bM = entity as BaseMountable;
                    var bP = entity as BasePlayer;

                    var bPlayer = bM.GetMounted();
                    var mountedPlayer = new List<BasePlayer>();
                    mountedPlayer.Add(bPlayer);

                    if (bP.isMounted && entity is BaseMountable)
                    {
                        if (Instance.config.shareSettings.staticQuarrySharing.localSharing.unlockStaticQuarryOnExitDome)
                        {
                            if (!mountedPlayer.Contains(bP) && staticQuarryToggler != bP.userID)
                            {
                                LockZone = false;
                                staticQuarryToggler = 0;
                            }
                            else
                            {
                                return;
                            }
                        }
                    }
                }
            }

            // A bypass check. If those things in the return are true then allow them to bypass/go through the sphere.
            private bool IsBypassAllowed(BasePlayer player)
            {
                return Instance.permission.UserHasPermission(player.UserIDString, "quarrymanager.allowbypass") || player.limitNetworking;
            }
        }

        #endregion
    }
}