using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using System.Collections;
using UnityEngine;
using Oxide.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using System;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using System.Threading.Tasks;
using System.Text;
using System.Text.RegularExpressions;

namespace Oxide.Plugins
{
    [Info("Quality Crafting", "mr01sam", "2.1.2")]
    [Description("Players can level crafting skills to produce higher quality items that have better stats than vanilla items.")]
    partial class QualityCrafting : CovalencePlugin
    {
        /* CHANGELOG
         *  - Updated for May 4th update
         *  - Fixed issue where storage containers would not update when moving items
         *  - Fixed OnInventoryNetwork update error
         */
        private static QualityCrafting PLUGIN;

        public const string PermissionAdmin = "qualitycrafting.admin";

        [PluginReference]
        private Plugin ImageLibrary;

        private Guid Secret { get; set; } = Guid.NewGuid();

        bool ServerInitialized { get; set; } = false;

        #region Initialization
        void Init()
        {
            UnsubscribeAll();
            PLUGIN = this;
            permission.RegisterPermission(PermissionAdmin, this);
        }

        void Unload()
        {
            SaveAll();
            foreach (var basePlayer in BasePlayer.activePlayerList)
            {
                UnloadPlayer(basePlayer);
            }
        }

        private void OnServerInitialized(bool initial)
        {
            try
            {
                ImageLibrary.Call("isLoaded", null);
            }
            catch (Exception)
            {
                PrintWarning($"The required dependency ImageLibary is not installed, {Name} will not work properly without it.");
                return;
            }
            LoadImages();
            LoadAll();
            LoadItemImages();
            foreach (var basePlayer in BasePlayer.activePlayerList)
            {
                LoadPlayer(basePlayer);
            }
            SubscribeAll();
            ServerInitialized = true;
        }

        void OnPlayerSleepEnded(BasePlayer basePlayer)
        {
            LoadPlayer(basePlayer);
        }

        object OnPlayerDeath(BasePlayer basePlayer, HitInfo info)
        {
            UnloadPlayer(basePlayer);
            return null;
        }

        void SubscribeAll()
        {
            Subscribe(nameof(Unload));
            Subscribe(nameof(OnPlayerSleepEnded));
            Subscribe(nameof(OnPlayerDeath));
            Subscribe(nameof(OnItemAction));
            Subscribe(nameof(OnItemCraft));
            Subscribe(nameof(OnItemCraftFinished));
            Subscribe(nameof(OnPlayerInput));
            Subscribe(nameof(OnActiveItemChanged));
            Subscribe(nameof(OnVendingShopOpened));
            Subscribe(nameof(OnLootEntity));
            Subscribe(nameof(OnLootEntityEnd));
            Subscribe(nameof(OnInventoryNetworkUpdate));
            Subscribe(nameof(OnLootNetworkUpdate));
            Subscribe(nameof(OnEntityTakeDamage));
            Subscribe(nameof(OnServerShutdown));
            Subscribe(nameof(OnServerSave));
        }

        void UnsubscribeAll()
        {
            Unsubscribe(nameof(Unload));
            Unsubscribe(nameof(OnPlayerSleepEnded));
            Unsubscribe(nameof(OnPlayerDeath));
            Unsubscribe(nameof(OnItemAction));
            Unsubscribe(nameof(OnItemCraft));
            Unsubscribe(nameof(OnItemCraftFinished));
            Unsubscribe(nameof(OnPlayerInput));
            Unsubscribe(nameof(OnActiveItemChanged));
            Unsubscribe(nameof(OnVendingShopOpened));
            Unsubscribe(nameof(OnLootEntity));
            Unsubscribe(nameof(OnLootEntityEnd));
            Unsubscribe(nameof(OnInventoryNetworkUpdate));
            Unsubscribe(nameof(OnLootNetworkUpdate));
            Unsubscribe(nameof(OnEntityTakeDamage));
            Unsubscribe(nameof(OnServerShutdown));
            Unsubscribe(nameof(OnServerSave));
        }

        void LoadPlayer(BasePlayer basePlayer)
        {
            RefreshOverlays(basePlayer);
            ShowAllButtons(basePlayer);
            ShowTrackingHud(basePlayer);
        }

        void UnloadPlayer(BasePlayer basePlayer)
        {
            DestroyAllOverlays(basePlayer);
            DestroyAllMenus(basePlayer);
            DestroyAllButtons(basePlayer);
            DestroyTrackingHud(basePlayer);
            NotificationManager.DestroyAllNotifications(basePlayer);
        }

        void OnServerShutdown()
        {
            SaveAll();
        }

        void OnServerSave()
        {
            SaveAll();
        }

        void LoadImages()
        {
            ImageLibrary.Call<bool>("AddImage", config.HUD.InspectButton.Icon, $"qc.inspect", 0UL);
            ImageLibrary.Call<bool>("AddImage", config.Qualities.IconQualityStar, $"qc.star", 0UL);
            ImageLibrary.Call<bool>("AddImage", config.Qualities.Tier1.Icon, $"qc.star.1", 0UL);
            ImageLibrary.Call<bool>("AddImage", config.Qualities.Tier2.Icon, $"qc.star.2", 0UL);
            ImageLibrary.Call<bool>("AddImage", config.Qualities.Tier3.Icon, $"qc.star.3", 0UL);
            ImageLibrary.Call<bool>("AddImage", config.Qualities.Tier4.Icon, $"qc.star.4", 0UL);
            ImageLibrary.Call<bool>("AddImage", config.Qualities.Tier5.Icon, $"qc.star.5", 0UL);
            ImageLibrary.Call<bool>("AddImage", config.HUD.SkillsButton.Icon, $"qc.button.skills", 0UL);
            ImageLibrary.Call<bool>("AddImage", config.HUD.InspectButton.Icon, $"qc.button.inspect", 0UL);
            ImageLibrary.Call<bool>("AddImage", config.HUD.QualityButton.Icon, $"qc.button.quality", 0UL);
            foreach (var category in SkillCategory.ALL)
            {
                ImageLibrary.Call<bool>("AddImage", config.Categories[category.NameTitleCase].Icon, $"qc.category.{category.Name}", 0UL);
            }
            ImageLibrary.Call<bool>("AddImage", config.Images.ArrowUp, $"qc.up", 0UL);
            ImageLibrary.Call<bool>("AddImage", config.Images.ArrowDown, $"qc.down", 0UL);
            ImageLibrary.Call<bool>("AddImage", config.Images.Close, $"qc.close", 0UL);
            ImageLibrary.Call<bool>("AddImage", config.Images.Help, $"qc.help", 0UL);
        }

        void LoadItemImages()
        {
            var url = "https://rustlabs.com/img/items180/";
            var itemList = ItemManager.itemList.ToDictionary(keySelector: m => m.shortname, elementSelector: m => $"{url}{m.shortname}");
            ImageLibrary.Call("ImportImageList", Name, itemList, 0, false, new Action(() =>
            {
            }));
        }

        void LoadAll()
        {
            CraftingManager.Load();
            TrackingManager.Load();
        }

        void SaveAll()
        {
            CraftingManager.Save();
            TrackingManager.Save();
        }
        #endregion
    }
}

namespace Oxide.Plugins
{
    partial class QualityCrafting : CovalencePlugin
    {
        private static readonly string PREFIX = "qc";

        #region Premade Arguments
        private static readonly CommandArgument SKILL_ARGUMENT = new CommandArgument
        {
            Parameter = "skill",
            Validate = (given) =>
            {
                SkillCategory category;
                category = SkillCategory.GetByName(given.ToLower());
                if (category == null)
                {
                    category = SkillCategory.GetByItemName(given);
                }
                return category == null ? new ValidationResponse(ValidationStatusCode.INVALID_VALUE, given) : new ValidationResponse();
            }
        };

        private static readonly CommandArgument CATEGORY_ARGUMENT = new CommandArgument
        {
            Parameter = "category",
            Validate = (given) =>
            {
                SkillCategory category;
                category = SkillCategory.GetByName(given.ToLower());
                return category == null ? new ValidationResponse(ValidationStatusCode.INVALID_VALUE, given) : new ValidationResponse();
            }
        };

        private static readonly CommandArgument QUALITY_ARGUMENT = new CommandArgument
        {
            Parameter = "quality",
            Validate = (given) =>
            {
                int intValue;
                if (int.TryParse(given, out intValue) && intValue >= 0 && intValue <= 5)
                {
                    return new ValidationResponse();
                }
                return new ValidationResponse(ValidationStatusCode.INVALID_VALUE, given);
            }
        };

        private static readonly CommandArgument XP_RATE_ARGUMENT = new CommandArgument
        {
            Parameter = "multiplier",
            Optional = true,
            Validate = (given) =>
            {
                float floatValue;
                if (float.TryParse(given, out floatValue) && floatValue >= 0)
                {
                    return new ValidationResponse();
                }
                return new ValidationResponse(ValidationStatusCode.INVALID_VALUE, given);
            }
        };

        private static readonly CommandArgument XP_ARGUMENT = new CommandArgument
        {
            Parameter = "xp",
            Validate = (given) =>
            {
                int intValue;
                if (int.TryParse(given, out intValue))
                {
                    return new ValidationResponse();
                }
                return new ValidationResponse(ValidationStatusCode.INVALID_VALUE, given);
            }
        };

        private static readonly CommandArgument LEVEL_ARGUMENT = new CommandArgument
        {
            Parameter = "level",
            Validate = (given) =>
            {
                int intValue;
                if (int.TryParse(given, out intValue) && intValue >= 0 && intValue <= 100)
                {
                    return new ValidationResponse();
                }
                return new ValidationResponse(ValidationStatusCode.INVALID_VALUE, given);
            }
        };
        #endregion

        #region Commands

        public static readonly List<CommandInfo> Commands = new List<CommandInfo>()
        {
            new CommandInfo()
            {
                Command = "help",
                Method = "CmdHelp",
                Description = "Opens the plugin help menu.",
                Rank = 1
            },
            new CommandInfo()
            {
                Command = "skills",
                Method = "CmdSkills",
                Description = "Opens the plugin skills menu.",
                Rank = 2
            },
            new CommandInfo()
            {
                Command = "buttons",
                Method = "CmdButtons",
                Description = "Toggles the visibility of the HUD buttons.",
                Arguments = new CommandArgument[]
                {
                    new CommandArgument
                    {
                        Parameter = "show/hide",
                        AllowedValues = new string[] {"show", "hide"}
                    }
                }
            },
            new CommandInfo()
            {
                Command = "wipeskills",
                Method = "CmdWipeSkills",
                Description = "Wipes all skill xp data for a specified player.",
                Permission = PermissionAdmin,
                Arguments = new CommandArgument[]
                {
                    CommandArgument.PLAYER_NAME
                },
            },
            new CommandInfo()
            {
                Command = "grantxp",
                Method = "CmdGrantXp",
                Description = "Grants the specified player crafting xp for the given item or category.",
                Permission = PermissionAdmin,
                Arguments = new CommandArgument[]
                {
                    CommandArgument.PLAYER_NAME,
                    SKILL_ARGUMENT,
                    XP_ARGUMENT
                }
            },
            new CommandInfo()
            {
                Command = "grantlevel",
                Method = "CmdGrantLevel",
                Description = "Advances the crafting skill level of the specified player by the given amount.",
                Permission = PermissionAdmin,
                Arguments = new CommandArgument[]
                {
                    CommandArgument.PLAYER_NAME,
                    SKILL_ARGUMENT,
                    LEVEL_ARGUMENT
                }
            },
            new CommandInfo()
            {
                Command = "setlevel",
                Method = "CmdSetLevel",
                Description = "Sets the crafting skill level for the given item or category.",
                Permission = PermissionAdmin,
                Arguments = new CommandArgument[]
                {
                    CommandArgument.PLAYER_NAME,
                    SKILL_ARGUMENT,
                    LEVEL_ARGUMENT
                }
            },
            new CommandInfo()
            {
                Command = "setquality",
                Method = "CmdSetQuality",
                Description = "Sets the quality level of an item. If no item id is given, the current active item will be targeted.",
                Permission = PermissionAdmin,
                Arguments = new CommandArgument[]
                {
                    CommandArgument.PLAYER_NAME,
                    QUALITY_ARGUMENT,
                    new CommandArgument
                    {
                        Parameter = "item uid",
                        Optional = true
                    }
                }
            },
            new CommandInfo()
            {
                Command = "xprate",
                Method = "CmdSetXpMultiplier",
                Description = "Temporarily overrides the configured xp multiplier for the given category. If a multiplier is not given, it will be reset to the default value.",
                Permission = PermissionAdmin,
                Arguments = new CommandArgument[]
                {
                    CATEGORY_ARGUMENT,
                    XP_RATE_ARGUMENT
                }
            },
            new CommandInfo()
            {
                Command = "getlevel",
                Method = "CmdGetLevel",
                Description = "Displays the crafting skill level information for the specified player",
                Permission = PermissionAdmin,
                Arguments = new CommandArgument[]
                {
                    CommandArgument.PLAYER_NAME,
                    SKILL_ARGUMENT
                }
            }
        };
        #endregion

        [Command("qc")]
        private void CmdController(IPlayer player, string command, string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    CmdHelp(player, command, args);
                    return;
                }
                var commandInfo = Commands.FirstOrDefault(x => x.Command == args[0]);
                if (commandInfo == null)
                {
                    CmdHelp(player, command, args);
                    return;
                }
                // Permission
                if (!player.IsAdmin && !player.IsServer)
                {
                    foreach (var perm in commandInfo.Permissions)
                    {
                        if (!permission.UserHasPermission(player.Id, perm))
                        {
                            player.Reply(Lang("no permission", player.Id));
                            return;
                        }
                    }
                }
                args = args.Skip(1).ToArray();
                // Validation
                var resp = commandInfo.Validate(args);
                if (resp.IsValid)
                {
                    commandInfo.Execute(player, command, args);
                }
                else
                {
                    string message = commandInfo.Usage(player, PREFIX);
                    switch (resp.StatusCode)
                    {

                        case ValidationStatusCode.PLAYER_NOT_FOUND:
                            message = Lang("player not found", player.Id, resp.Data);
                            break;
                        case ValidationStatusCode.INVALID_VALUE:
                        case ValidationStatusCode.VALUE_NOT_ALLOWED:
                            message = Lang("invalid value", player.Id, resp.Data);
                            break;
                        case ValidationStatusCode.SUCCESS:
                            message = "weird";
                            break;
                    }
                    player.Reply(message);
                }
            }
            catch (Exception)
            {
                Lang("command error", player.Id);
            }

        }

        private void CmdSkills(IPlayer player, string command, string[] args)
        {
            if (player.IsServer)
            {
                player.Reply(Lang("command success", player.Id));
                return;
            }
            BasePlayer basePlayer = player.Object as BasePlayer;
            ShowSkillsMenu(basePlayer, false);
        }

        private void CmdButtons(IPlayer player, string command, string[] args)
        {
            if (player.IsServer)
            {
                player.Reply(Lang("command success", player.Id));
                return;
            }
            var toggle = args[0];
            BasePlayer basePlayer = player.Object as BasePlayer;
            if (toggle == "show")
            {
                ShowAllButtons(basePlayer);
            }
            else
            {
                DestroyAllButtons(basePlayer);
            }
            player.Reply(Lang("command success", player.Id));
        }

        private void CmdHelp(IPlayer player, string command, string[] args)
        {
            if (player.IsServer)
            {
                Puts("\n" + string.Join("\n", Commands.Select(x => $"{PREFIX} {x.Command} {x.ArgString}".PadRight(60) + x.Description)));
            }
            else
            {
                BasePlayer basePlayer = player.Object as BasePlayer;
                ShowHelpMenu(basePlayer, false, 1);
            }
        }

        private void CmdWipeSkills(IPlayer player, string command, string[] args)
        {
            var name = args[0];
            var target = BasePlayer.FindAwakeOrSleeping(name);
            CraftingManager.Clear(target);
            player.Reply(Lang("command success", player.Id));
        }

        private void CmdGrantXp(IPlayer player, string command, string[] args)
        {
            var name = args[0];
            var skill = args[1].ToLower();
            var xp = uint.Parse(args[2]);
            var target = BasePlayer.FindAwakeOrSleeping(name);
            var icon = "";
            var displayName = "";
            var oldLevel = 0;
            var newLevel = 0;
            var oldXp = 0f;
            var newXp = 0f;
            PlayerSkillSheet skills = CraftingManager.GetSkills(target);
            SkillCategory category = SkillCategory.GetByName(skill);
            if (category == null)
            {
                var itemDef = ItemManager.itemList.FirstOrDefault(x => x.displayName.translated.ToLower() == skill);
                category = SkillCategory.GetByItemDefinition(itemDef);
                var item = ItemManager.Create(itemDef);
                oldLevel = skills.GetLevel(item);
                oldXp = skills.GetLevelPercent(item);
                skills.GrantXP(item, xp);
                newLevel = skills.GetLevel(item);
                newXp = skills.GetLevelPercent(item);
                icon = item.info.shortname;
                displayName = item.info.displayName.translated;
            }
            else
            {
                oldLevel = skills.GetLevel(category);
                oldXp = skills.GetLevelPercent(category);
                skills.GrantXP(category, xp);
                ShowTrackingHud(target);
                newLevel = skills.GetLevel(category);
                newXp = skills.GetLevelPercent(category);
                icon = $"qc.category.{skill}";
                displayName = category.DisplayName(target);
            }
            NotificationManager.AddNotifications(target, new GainedXPNotification
            {
                Icon = icon,
                SkillDisplayName = displayName,
                OldLevel = oldLevel,
                NewLevel = newLevel,
                OldXP = oldXp,
                NewXP = newXp,
                IsLevelUp = newLevel > oldLevel,
                XPGained = xp
            });
            player.Reply(Lang("command success", player.Id));
        }

        private void CmdSetLevel(IPlayer player, string command, string[] args)
        {
            var name = args[0];
            var target = BasePlayer.FindAwakeOrSleeping(name);
            var skill = args[1].ToLower();
            var level = int.Parse(args[2]);
            PlayerSkillSheet skills = CraftingManager.GetSkills(target);
            SkillCategory category = SkillCategory.GetByName(skill);
            if (category == null)
            {
                var itemDef = ItemManager.itemList.FirstOrDefault(x => x.displayName.translated.ToLower() == skill);
                category = SkillCategory.GetByItemDefinition(itemDef);
                var item = ItemManager.Create(itemDef);
                skills.SetLevel(item, level);
            }
            else
            {
                skills.SetLevel(category, level);
                ShowTrackingHud(target);
            }
            player.Reply(Lang("command success", player.Id));
        }

        private void CmdSetQuality(IPlayer player, string command, string[] args)
        {
            var name = args[0];
            var target = BasePlayer.FindAwakeOrSleeping(name);
            var quality = int.Parse(args[1]);
            Item item;
            if (args.Length == 2)
            {
                item = target.GetActiveItem();
            }
            else
            {
                var uid = ulong.Parse(args[2]);
                item = target.inventory.FindItemUID(new ItemId(uid));
            }
            if (item != null)
            {
                var qi = new QualityItem(item);
                BasePlayer creator = null;
                if (qi.HasCreator)
                {
                    creator = BasePlayer.FindAwakeOrSleeping(qi.CreatorId.ToString());
                }
                QualityItemManager.SetItemQuality(item, quality, creator);
                QualityItemManager.ApplyQualityModifiers(item, quality);
            }
            player.Reply(Lang("command success", player.Id));
        }

        private void CmdSetXpMultiplier(IPlayer player, string command, string[] args)
        {
            var categoryName = args[0];
            var category = SkillCategory.GetByName(categoryName);
            if (args.Length == 1)
            {
                category.TemporaryMultiplier = null;
            }
            else
            {
                var multiplier = float.Parse(args[1]);
                category.TemporaryMultiplier = multiplier;
            }
            player.Reply(Lang("command success", player.Id));
        }

        private void CmdGrantLevel(IPlayer player, string command, string[] args)
        {
            var playerName = args[0];
            var basePlayer = BasePlayer.FindAwakeOrSleeping(playerName);
            var skillName = args[1];
            var amount = int.Parse(args[2]);
            var category = SkillCategory.GetByName(skillName);
            var skills = CraftingManager.GetSkills(basePlayer);
            int level;
            if (category == null)
            {
                var item = ItemManager.Create(ItemManager.itemList.FirstOrDefault(x => x.displayName.translated.ToLower() == skillName.ToLower()));
                level = skills.GetLevel(item);
                skills.SetLevel(item, Math.Min(100, level + amount));
            }
            else
            {
                level = skills.GetLevel(category);
                skills.SetLevel(category, Math.Min(100, level + amount));
            }
            player.Reply(Lang("command success", player.Id));
        }

        private void CmdGetLevel(IPlayer player, string command, string[] args)
        {
            var playerName = args[0];
            var basePlayer = BasePlayer.FindAwakeOrSleeping(playerName);
            var skillName = args[1];
            int level;
            string displayName;
            var skills = CraftingManager.GetSkills(basePlayer);
            var category = SkillCategory.GetByName(skillName);
            if (category == null)
            {
                var item = ItemManager.Create(ItemManager.itemList.FirstOrDefault(x => x.displayName.translated.ToLower() == skillName.ToLower()));
                level = skills.GetLevel(item);
                displayName = item.info.displayName.translated;
            }
            else
            {
                level = skills.GetLevel(category);
                displayName = category.DisplayName(basePlayer);
            }
            player.Reply($"{basePlayer.displayName} {displayName} {level}");
        }
    }
}

namespace Oxide.Plugins
{
	partial class QualityCrafting : CovalencePlugin
	{
		private Configuration config;

		private partial class Configuration
		{
			[JsonProperty(PropertyName = "Version")]
			public VersionNumber Version { get; set; } = new VersionNumber(0, 0, 0);

			[JsonProperty(PropertyName = "General")]
			public GeneralConfig Settings { get; set; } = new GeneralConfig();

			[JsonProperty(PropertyName = "Categories")]
			public Dictionary<string, CraftingCategoryConfig> Categories { get; set; } = SkillCategory.ALL.ToDictionary(x => x.Name.TitleCase(), x => new CraftingCategoryConfig()
			{
				Icon = x.DefaultIcon
			});

			[JsonProperty(PropertyName = "Quality Tiers")]
			public QualityConfig Qualities { get; set; } = new QualityConfig();

			[JsonProperty(PropertyName = "Notifications")]
			public NotificationConfig Notifications = new NotificationConfig();

			[JsonProperty(PropertyName = "HUD")]
			public HUDConfig HUD = new HUDConfig();

			[JsonProperty(PropertyName = "Colors")]
			public ColorsConfig Colors = new ColorsConfig();

			[JsonProperty(PropertyName = "Sounds")]
			public SFXConfig SFX = new SFXConfig();

			[JsonProperty(PropertyName = "UI Images")]
			public ImagesConfig Images = new ImagesConfig();
		}

		public class GeneralConfig
        {
			[JsonProperty(PropertyName = "Blueprint XP Gain")]
			public bool BlueprintXPGain = true;
		}

		public class HUDConfig
		{
			[JsonProperty(PropertyName = "Inspect Button")]
			public HUDButton InspectButton = new HUDButton
			{
				Icon = "https://i.imgur.com/tPi2qM4.png",
				X = 407,
				Y = 19,
				Size = 25
			};

			[JsonProperty(PropertyName = "Quality Button")]
			public HUDButton QualityButton = new HUDButton
			{
				Icon = "https://i.imgur.com/KMWMn0K.png",
				X = 407,
				Y = 52,
				Size = 25
			};

			[JsonProperty(PropertyName = "Skills Button")]
			public HUDButton SkillsButton = new HUDButton
			{
				Icon = "https://i.imgur.com/x0Zg12R.png",
				X = 830,
				Y = 30,
				Size = 32
			};

			[JsonProperty(PropertyName = "Tracked Skill")]
			public HUDElement TrackedSkill = new HUDElement
			{
				X = 890,
				Y = 17,
				Size = 160
			};
		}
		public class CraftingCategoryConfig
		{
			[JsonProperty(PropertyName = "Enabled")]
			public bool Enabled { get; set; } = true;
			[JsonProperty(PropertyName = "Icon")]
			public string Icon { get; set; }

			[JsonProperty(PropertyName = "Base XP Multiplier")]
			public float XPMultiplier { get; set; } = 1f;

			[JsonProperty(PropertyName = "Base Crafting Speed")]
			public float BaseCraftingSpeed { get; set; } = 1f;

			[JsonProperty(PropertyName = "Perk Increases Per Level")]
			public CraftingCategoryPerkConfig PerkIncreasesPerLevel { get; set; } = new CraftingCategoryPerkConfig();
		}

		public class CraftingCategoryPerkConfig
        {
			[JsonProperty(PropertyName = "Crafting Speed")]
			public float CraftingSpeed { get; set; } = 0.04f;

			[JsonProperty(PropertyName = "Duplicate Chance")]
			public float DuplicateChance { get; set; } = 0.002f;
		}

		public class HUDButton
        {
			[JsonProperty(PropertyName = "Icon")]
			public string Icon { get; set; }
			[JsonProperty(PropertyName = "X")]
			public int X { get; set; }
			[JsonProperty(PropertyName = "Y")]
			public int Y { get; set; }
			[JsonProperty(PropertyName = "Size")]
			public int Size { get; set; }
		}

		public class HUDElement
		{
			[JsonProperty(PropertyName = "X")]
			public int X { get; set; }
			[JsonProperty(PropertyName = "Y")]
			public int Y { get; set; }
			[JsonProperty(PropertyName = "Size")]
			public int Size { get; set; }
		}

		public class ImagesConfig
		{
			[JsonProperty(PropertyName = "Close")]
			public string Close { get; set; } = "https://i.imgur.com/AbG6hrk.png";

			[JsonProperty(PropertyName = "Help")]
			public string Help { get; set; } = "https://i.imgur.com/tVSQyuX.png";

			[JsonProperty(PropertyName = "Arrow Up")]
			public string ArrowUp { get; set; } = "https://i.imgur.com/Mgua5IP.png";

			[JsonProperty(PropertyName = "Arrow Down")]
			public string ArrowDown { get; set; } = "https://i.imgur.com/L5kufsD.png";
		}

		public class QualityConfig
		{
			[JsonProperty(PropertyName = "Star Icon")]
			public string IconQualityStar = "https://imgur.com/fbELboi.png";

			[JsonProperty(PropertyName = "Tier 0")]
			public QualityTierConfig Tier0 = new QualityTierConfig()
			{
				Color = "0.9 0.9 0.9 1"
			};

			[JsonProperty(PropertyName = "Tier 1")]
			public QualityTierConfig Tier1 = new QualityTierConfig()
			{
				Icon = "https://imgur.com/BKx4Hs8.png",
				Color = "0.11764 1 0 1"
			};

			[JsonProperty(PropertyName = "Tier 2")]
			public QualityTierConfig Tier2 = new QualityTierConfig()
			{
				Icon = "https://imgur.com/NSZTZ4v.png",
				Color = "0 0.43921 1 1"
			};

			[JsonProperty(PropertyName = "Tier 3")]
			public QualityTierConfig Tier3 = new QualityTierConfig()
			{
				Icon = "https://imgur.com/XSbgf72.png",
				Color = "0.63921 0.20784 0.93333 1"
			};

			[JsonProperty(PropertyName = "Tier 4")]
			public QualityTierConfig Tier4 = new QualityTierConfig()
			{
				Icon = "https://imgur.com/cEZUU9F.png",
				Color = "0.87531 0.70196 0 1"
			};

			[JsonProperty(PropertyName = "Tier 5")]
			public QualityTierConfig Tier5 = new QualityTierConfig()
			{
				Icon = "https://imgur.com/RD7ED4R.png",
				Color = "1 0.29803 0.14901 1"
			};
			[JsonProperty(PropertyName = "Percent Stat Increases Per Tier")]
			public ModifiersConfig Modifiers = new ModifiersConfig();
		}

		public class QualityTierConfig
        {
			[JsonProperty(PropertyName = "Icon")]
			public string Icon { get; set; } = null;

			[JsonProperty(PropertyName = "Color")]
			public string Color { get; set; }
		}

		public class ModifiersConfig
		{
			[JsonProperty(PropertyName = "Projectile Damage")]
			public float ProjectileDamage = 0.08f;
			[JsonProperty(PropertyName = "Protection")]
			public float Protection = 0.01f;
			[JsonProperty(PropertyName = "Melee Damage")]
			public float MeleeDamage = 0.20f;
			[JsonProperty(PropertyName = "Durability")]
			public float Durability = 0.20f;
			[JsonProperty(PropertyName = "Gather Rate")]
			public float GatherRate = 0.10f;
		}

		public class NotificationConfig
        {

			[JsonProperty(PropertyName = "Item Crafted Notification")]
			public NotificationUIConfig ItemCraftedNotification = new NotificationUIConfig()
			{
				Show = true,
				X = 480,
				Y = 124
			};

			[JsonProperty(PropertyName = "XP Gained Notification")]
			public NotificationUIConfig LevelUpItemNotification = new NotificationUIConfig()
			{
				Show = true,
				X = 480,
				Y = 124
			};
		}

		public class NotificationUIConfig
        {
			[JsonProperty(PropertyName = "Show")]
			public bool Show;
			[JsonProperty(PropertyName = "X")]
			public int X;
			[JsonProperty(PropertyName = "Y")]
			public int Y;
		}

		public class ColorsConfig
        {
			[JsonProperty(PropertyName = "HUD Background")]
			public string HUDBackground { get; set; } = "0.5 0.5 0.5 0.5";

			[JsonProperty(PropertyName = "Menu Background")]
			public string MenuBackground { get; set; } = "0.16078 0.16078 0.12941 1";

			[JsonProperty(PropertyName = "HUD Button Toggled")]
			public string HUDButtonToggled { get; set; } = "1 1 1 1";

			[JsonProperty(PropertyName = "HUD Button Untoggled")]
			public string HUDButtonUntoggled { get; set; } = "1 1 1 0.4";

			[JsonProperty(PropertyName = "XP Bar")]
			public string XPBar { get; set; } = "0.5 1 0.5 1";

			[JsonProperty(PropertyName = "Text")]
			public string Text { get; set; } = "1 1 1 0.4";
		}

		public class SFXConfig
		{
			[JsonProperty(PropertyName = "Item Crafted Normal")]
			public string ItemCraftedNormal { get; set; } = "assets/bundled/prefabs/fx/notice/loot.start.fx.prefab";

			[JsonProperty(PropertyName = "Item Crafted Rare")]
			public string ItemCraftedRare { get; set; } = "assets/prefabs/deployable/research table/effects/research-success.prefab";

			[JsonProperty(PropertyName = "Item Duplicated")]
			public string ItemDuplicated { get; set; } = "assets/bundled/prefabs/fx/item_unlock.prefab";

			[JsonProperty(PropertyName = "Skill Level Up")]
			public string SkillLevelUp { get; set; } = "assets/prefabs/locks/keypad/effects/lock.code.updated.prefab";
		}

		protected override void LoadConfig()
		{
			base.LoadConfig();
			var recommended = "It is recommended to backup your current configuration file and remove it to generate a fresh one.";
			var usingDefault = "Overriding configuration with default values to avoid errors.";

            try
            {
                config = Config.ReadObject<Configuration>();
				if (config == null) { throw new Exception(); }
				if (config.Version.Major <= 0)
                {
					config.Version = new VersionNumber(Version.Major, Version.Minor, Version.Patch);
				}
				else if (config.Version.Major > 0 && config.Version.Major != Version.Major || config.Version.Minor != Version.Minor) throw new NotSupportedException();
				SaveConfig();
			}
            catch (NotSupportedException)
            {
				PrintError($"Your configuration file is out of date. Your configuration file is for v{config.Version.Major}.{config.Version.Minor}.{config.Version.Patch} but the plugin is on v{Version.Major}.{Version.Minor}.{Version.Patch}. {recommended}");
				PrintWarning(usingDefault);
				LoadDefaultConfig();
			}
			catch(Exception)
            {
				PrintError($"Your configuration file contains an error. {recommended}");
				PrintWarning(usingDefault);
				LoadDefaultConfig();
			}
			//PrintError($"DEFAULT CONFIG LOADED REMOVE!!!!");
			//LoadDefaultConfig(); // TODO Comment this out
		}

		protected override void SaveConfig() => Config.WriteObject(config);

		protected override void LoadDefaultConfig() => config = new Configuration();

	}
}

namespace Oxide.Plugins
{
    partial class QualityCrafting : CovalencePlugin
    {
        object OnItemAction(Item bp, string action, BasePlayer basePlayer)
        {
            if (basePlayer != null && bp != null && bp.IsBlueprint() && action == "study" && basePlayer.blueprints != null && config.Settings.BlueprintXPGain && basePlayer.blueprints.HasUnlocked(bp.blueprintTargetDef))
            {
                bp.UseItem(1);
                var item = ItemManager.Create(bp.blueprintTargetDef);
                var category = SkillCategory.GetByItemDefinition(item.info);
                var skills = CraftingManager.GetSkills(basePlayer);
                int oldItemLevel = skills.GetLevel(item);
                int catLevel = skills.GetLevel(category);
                float catXp = skills.GetLevelPercent(category);
                float oldItemXp = skills.GetLevelPercent(item);
                uint xp = QualityItemManager.GetItemXpRate(item);
                skills = skills.GrantXP(item, xp);
                int newItemLevel = skills.GetLevel(item);
                float newItemXp = skills.GetLevelPercent(item);
                NotificationManager.AddNotifications(basePlayer, new GainedXPNotification
                {
                    Icon = item.info.shortname,
                    SkillDisplayName = item.info.displayName.translated,
                    OldLevel = oldItemLevel,
                    NewLevel = newItemLevel,
                    OldXP = oldItemXp,
                    NewXP = newItemXp,
                    IsLevelUp = newItemLevel > oldItemLevel,
                    XPGained = xp
                });
            }
            return null;
        }

        object OnItemCraft(ItemCraftTask task, BasePlayer basePlayer, Item item)
        {
            if (task != null && task.blueprint != null && task.blueprint.targetItem != null && basePlayer != null)
            {
                var category = SkillCategory.GetByItemDefinition(task.blueprint.targetItem);
                if (category != null)
                {
                    var skills = CraftingManager.GetSkills(basePlayer);
                    if (skills != null)
                    {
                        var baseSpeed = category.GetBaseCraftingSpeed();
                        task.blueprint = UnityEngine.Object.Instantiate(task.blueprint);
                        var speed = skills.GetCraftingSpeedMultiplier(category) * baseSpeed;
                        task.blueprint.time *= (1f / speed);
                    }
                }
            }
            return null;
        }

        void OnItemCraftFinished(ItemCraftTask task, Item item)
        {
            BasePlayer basePlayer = task.owner;
            if (basePlayer != null && item != null)
            {
                SkillCategory category = SkillCategory.GetByItem(item);
                if (category != null)
                {
                    var skills = CraftingManager.GetSkills(basePlayer);
                    var duplicate = skills.GetDuplicateChance(category) >= UnityEngine.Random.Range(0, 100);
                    int quality = CraftingManager.GetCraftedItemQuality(item, skills);
                    int oldCatLevel = skills.GetLevel(category);
                    int oldItemLevel = skills.GetLevel(item);
                    float oldCatXp = skills.GetLevelPercent(category);
                    float oldItemXp = skills.GetLevelPercent(item);
                    var itemXpRate = QualityItemManager.GetItemXpRate(item);
                    var dupeRate = duplicate ? 2f : 1f;
                    var xpMult = category.GetXpMultiplier();
                    var itemXpGained = (uint) ((10 + (quality * 2)) * dupeRate * xpMult);
                    var catXpGained = (uint) (itemXpRate * dupeRate * xpMult);
                    skills = skills.GrantXP(category, catXpGained);
                    ShowTrackingHud(basePlayer);
                    skills = skills.GrantXP(item, itemXpGained);
                    int newCatLevel = skills.GetLevel(category);
                    int newItemLevel = skills.GetLevel(item);
                    float newCatXp = skills.GetLevelPercent(category);
                    float newItemXp = skills.GetLevelPercent(item);
                    QualityItemManager.SetItemQuality(item, quality, basePlayer);
                    QualityItemManager.ApplyQualityModifiers(item, quality);
                    string qualityColor = QualityItemManager.GetColorByQuality(quality);
                    bool isRare = QualityItemManager.IsRareQuality(quality, oldItemLevel);
                    if (duplicate)
                    {
                        var newItem = ItemManager.CreateByItemID(item.info.itemid);
                        QualityItemManager.SetItemQuality(newItem, quality, basePlayer);
                        QualityItemManager.ApplyQualityModifiers(newItem, quality);
                        basePlayer.GiveItem(newItem);
                    }
                    bool catLevelUp = newCatLevel > oldCatLevel;
                    bool itemLevelUp = newItemLevel > oldItemLevel;
                    NotificationManager.AddNotifications(basePlayer, new ItemCraftedNotification
                    {
                        IsItem = true,
                        Icon = item.info.shortname,
                        SkillDisplayName = item.info.displayName.translated,
                        OldCategoryLevel = oldCatLevel,
                        NewCategoryLevel = newCatLevel,
                        OldItemLevel = oldItemLevel,
                        NewItemLevel = newItemLevel,
                        OldCategoryXP = oldCatXp,
                        NewCategoryXP = newCatXp,
                        OldItemXP = oldItemXp,
                        NewItemXP = newItemXp,
                        IsCategoryLevelup = catLevelUp,
                        IsItemLevelup = itemLevelUp,
                        CategoryXPGained = catXpGained,
                        ItemXPGained = itemXpGained,
                        Quality = quality,
                        Category = category.Name,
                        IsRare = isRare,
                        IsDuplicated = duplicate
                    });
                }
            }
        }
    }
}

namespace Oxide.Plugins
{
    partial class QualityCrafting : CovalencePlugin
    {
        [Command("qc.help")]
        private void Cmdhelp(IPlayer player, string command, string[] args)
        {
            var check = new SecurityCheck(args);
            if (!check.Success) { return; } else { args = check.Args; }
            var basePlayer = player.Object as BasePlayer;
            bool autoHide = bool.Parse(args[0]);
            ShowHelpMenu(basePlayer, autoHide, 0);
        }

        [Command("qc.help.tab")]
        private void CmdHelpTab(IPlayer player, string command, string[] args)
        {
            var check = new SecurityCheck(args);
            if (!check.Success) { return; } else { args = check.Args; }
            if (args.Length != 2)
            {
                return;
            }
            var basePlayer = player.Object as BasePlayer;
            bool autoHide = bool.Parse(args[0]);
            var index = int.Parse(args[1]);
            ShowHelpMenu(basePlayer, autoHide, index);
        }


        private void DestroyHelpMenu(BasePlayer basePlayer)
        {
            CuiHelper.DestroyUi(basePlayer, HELP_MENU_OVERLAY);
            CuiHelper.DestroyUi(basePlayer, HELP_MENU);
        }

        private void ShowHelpMenu(BasePlayer basePlayer, bool autoHide = true, int index = 0)
        {
            var container = new CuiElementContainer();
            int width = 800;
            int height = 500;
            int padding = 10;
            int closeButtonSize = 15;
            // Overlay
            var overlay = new CuiElement
            {
                Parent = "Overlay",
                Name = HELP_MENU_OVERLAY,
                Components =
                {
                    new CuiButtonComponent
                    {
                        Command = $"qc.menu.close {Secret}",
                        Color = "0 0 0 0.9",
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }
                }
            };
            if (!autoHide)
            {
                overlay.Components.Add(new CuiNeedsCursorComponent());
            }
            container.Add(overlay);
            // Base
            container.Add(new CuiElement
            {
                Parent = "Overlay",
                Name = HELP_MENU,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = config.Colors.MenuBackground,
                        Material = "assets/scenes/test/waterlevelterrain/watertexture.png",
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = $"{-width/2} {-height/2}",
                        OffsetMax = $"{width/2} {height/2}"
                    }
                }
            });
            // Close Button
            #region CloseBtn
            container.Add(new CuiElement
            {
                Parent = HELP_MENU,
                Name = $"{HELP_MENU}.close",
                Components = {
                    new CuiButtonComponent
                    {
                        Command = $"qc.menu.close {Secret}",
                        Color = "0 0 0 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "1 1",
                        AnchorMax = "1 1",
                        OffsetMin = $"{-padding - closeButtonSize} {-padding - closeButtonSize}",
                        OffsetMax = $"{-padding} {-padding}"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = $"{HELP_MENU}.close",
                Components = {
                    new CuiImageComponent
                    {
                        Png = ImageLibrary?.Call<string>("GetImage", $"qc.close"),
                        Color = config.Colors.Text
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = $"{3} {3}",
                        OffsetMax = $"{-3} {-3}"
                    }
                }
            });
            #endregion

            #region Return Button
            container.Add(new CuiElement
            {
                Parent = HELP_MENU,
                Name = $"{HELP_MENU}.return",
                Components = {
                    new CuiButtonComponent
                    {
                        Command = $"qc.menu.show {Secret} {autoHide}",
                        Color = "0 0 0 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "1 1",
                        AnchorMax = "1 1",
                        OffsetMin = $"{-2*padding - 2*closeButtonSize} {-padding - closeButtonSize}",
                        OffsetMax = $"{-2*padding - closeButtonSize} {-padding}"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = $"{HELP_MENU}.return",
                Components = {
                    new CuiImageComponent
                    {
                        Png = ImageLibrary?.Call<string>("GetImage", $"qc.button.skills"),
                        Color = config.Colors.Text
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = $"{3} {3}",
                        OffsetMax = $"{-3} {-3}"
                    }
                }
            });
            #endregion

            // Title
            container.Add(new CuiElement
            {
                Parent = HELP_MENU,
                Components = {
                    new CuiTextComponent
                    {
                        Text = Lang("plugin help", basePlayer),
                        Align = UnityEngine.TextAnchor.UpperCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "1 1",
                        OffsetMin = $"{padding+100} {-padding-100}",
                        OffsetMax = $"{-padding-100} {-padding}"
                    }
                }
            });
            // Toolbar
            var toolbarId = $"{HELP_MENU}.toolbar";
            var toolBarS = 25;
            var toolbarH = 20;
            var totalToolBarH = toolBarS + toolbarH;
            container.Add(new CuiElement
            {
                Parent = HELP_MENU,
                Name = toolbarId,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0",
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "1 1",
                        OffsetMin = $"{padding} {-padding-totalToolBarH}",
                        OffsetMax = $"{-padding} {-padding-toolBarS}"
                    }
                }
            });
            // Tab 1 - Overview
            var tabW = 60;
            var tabP = 4;
            var tabLeft = 0;
            var tabColor = "0 0 0 0.6";
            var tabHighlightedColor = "0 0 0 0.2";
            var tabTextColor = "1 1 1 1";
            var tabTextSize = 10;
            var selected = index;
            #region Tab1
            var tab1Id = $"{toolbarId}.tab.1";
            container.Add(new CuiElement
            {
                Name = tab1Id,
                Parent = toolbarId,
                Components = {
                    new CuiButtonComponent
                    {
                        Command = $"qc.help.tab {Secret} {autoHide} 0",
                        Color = selected == 0 ? tabHighlightedColor : tabColor,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0 1",
                        OffsetMin = $"{tabLeft} {0}",
                        OffsetMax = $"{tabLeft + tabW} {0}"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = tab1Id,
                Components = {
                    new CuiTextComponent
                    {
                        Text = Lang("overview", basePlayer),
                        Align = UnityEngine.TextAnchor.MiddleCenter,
                        FontSize = tabTextSize,
                        Color = tabTextColor
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }
                }
            });
            tabLeft += tabW + tabP;
            #endregion

            // Tab 2 - Commands
            #region Tab2
            var tab2Id = $"{toolbarId}.tab.2";
            container.Add(new CuiElement
            {
                Name = tab2Id,
                Parent = toolbarId,
                Components = {
                    new CuiButtonComponent
                    {
                        Command = $"qc.help.tab {Secret} {autoHide} 1",
                        Color = selected == 1 ? tabHighlightedColor : tabColor,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0 1",
                        OffsetMin = $"{tabLeft} {0}",
                        OffsetMax = $"{tabLeft + tabW} {0}"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = tab2Id,
                Components = {
                    new CuiTextComponent
                    {
                        Text = Lang("commands", basePlayer),
                        Align = UnityEngine.TextAnchor.MiddleCenter,
                        FontSize = tabTextSize,
                        Color = tabTextColor
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }
                }
            });
            tabLeft += tabW + tabP;
            #endregion

            // Tab 3 - FAQ
            #region Tab2
            //var tab3Id = $"{toolbarId}.tab.3";
            //container.Add(new CuiElement
            //{
            //    Name = tab3Id,
            //    Parent = toolbarId,
            //    Components = {
            //        new CuiButtonComponent
            //        {
            //            Command = $"qc.help.tab {Secret} {autoHide} 2",
            //            Color = selected == 2 ? tabHighlightedColor : tabColor,
            //        },
            //        new CuiRectTransformComponent
            //        {
            //            AnchorMin = "0 0",
            //            AnchorMax = "0 1",
            //            OffsetMin = $"{tabLeft} {0}",
            //            OffsetMax = $"{tabLeft + tabW} {0}"
            //        }
            //    }
            //});
            //container.Add(new CuiElement
            //{
            //    Parent = tab3Id,
            //    Components = {
            //        new CuiTextComponent
            //        {
            //            Text = "FAQ",
            //            Align = UnityEngine.TextAnchor.MiddleCenter,
            //            FontSize = tabTextSize,
            //            Color = tabTextColor
            //        },
            //        new CuiRectTransformComponent
            //        {
            //            AnchorMin = "0 0",
            //            AnchorMax = "1 1"
            //        }
            //    }
            //});
            //tabLeft += tabW + tabP;
            #endregion

            // Tab 4 - Support
            #region Tab2
            var tab4Id = $"{toolbarId}.tab.4";
            container.Add(new CuiElement
            {
                Name = tab4Id,
                Parent = toolbarId,
                Components = {
                    new CuiButtonComponent
                    {
                        Command = $"qc.help.tab {Secret} {autoHide} 3",
                        Color = selected == 3 ? tabHighlightedColor : tabColor,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0 1",
                        OffsetMin = $"{tabLeft} {0}",
                        OffsetMax = $"{tabLeft + tabW} {0}"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = tab4Id,
                Components = {
                    new CuiTextComponent
                    {
                        Text = Lang("support", basePlayer),
                        Align = UnityEngine.TextAnchor.MiddleCenter,
                        FontSize = tabTextSize,
                        Color = tabTextColor
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }
                }
            });
            tabLeft += tabW + tabP;
            #endregion

            // Content
            var contentId = $"{HELP_MENU}.content";
            container.Add(new CuiElement
            {
                Parent = HELP_MENU,
                Name = contentId,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0.2",
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = $"{padding} {padding}",
                        OffsetMax = $"{-padding} {-padding-totalToolBarH-padding}"
                    }
                }
            });

            if (index == 0)
            {
                container = CreateOverviewHelpTab(basePlayer, container, contentId, padding);
            }
            if (index == 1)
            {
                container = CreateCommandsHelpTab(basePlayer, container, contentId, padding);
            }
            if (index == 3)
            {
                container = CreateSupportTab(basePlayer, container, contentId, padding);
            }

            DestroyAllMenus(basePlayer);
            CuiHelper.AddUi(basePlayer, container);
            MenuToggled[basePlayer.UserIDString] = true;
        }

        private CuiElementContainer CreateOverviewHelpTab(BasePlayer basePlayer, CuiElementContainer container, string parent, int padding)
        {
            var halfPad = (padding / 2f);
            var panelColor = "0 0 0 0.6";
            var titleTextSize = 16;
            var titleTextColor = "1 1 1 1";
            var bodyTextSize = 14;
            var bodyTextColor = config.Colors.Text;

            // Left
            #region Left
            var leftId = $"{parent}.left";
            container.Add(new CuiElement
            {
                Parent = parent,
                Name = leftId,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = panelColor,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0.333 1",
                        OffsetMin = $"{0} {0}",
                        OffsetMax = $"{-halfPad} {0}"
                    }
                }
            });
            // Title
            container.Add(new CuiElement
            {
                Parent = leftId,
                Components = {
                    new CuiTextComponent
                    {
                        Text = Lang("level up skills", basePlayer),
                        Align = UnityEngine.TextAnchor.UpperCenter,
                        FontSize = titleTextSize,
                        Color = titleTextColor
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0.8",
                        AnchorMax = "1 1",
                        OffsetMin = $"{0} {0}",
                        OffsetMax = $"{0} {-padding}"
                    }
                }
            });
            // Image
            var imgSize = 100;
            container.Add(new CuiElement
            {
                Parent = leftId,
                Components = {
                    new CuiImageComponent
                    {
                        Png = ImageLibrary?.Call<string>("GetImage", $"qc.button.skills"),
                        Color = "0 0.5 0.9 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.8",
                        AnchorMax = "0.5 0.8",
                        OffsetMin = $"{-imgSize/2} {-imgSize}",
                        OffsetMax = $"{imgSize/2} {0}"
                    }
                }
            });
            // Info Text
            container.Add(new CuiElement
            {
                Parent = leftId,
                Components = {
                    new CuiTextComponent
                    {
                        Text = Lang("craft items or study", basePlayer),
                        Align = UnityEngine.TextAnchor.UpperCenter,
                        FontSize = bodyTextSize,
                        Color = bodyTextColor
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 0.7",
                        OffsetMin = $"{padding} {padding}",
                        OffsetMax = $"{-padding} {-imgSize-padding}"
                    }
                }
            });
            #endregion

            // Middle
            #region Middle
            var middleId = $"{parent}.middle";
            container.Add(new CuiElement
            {
                Parent = parent,
                Name = middleId,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = panelColor,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.333 0",
                        AnchorMax = "0.667 1",
                        OffsetMin = $"{halfPad} {0}",
                        OffsetMax = $"{-halfPad} {0}"
                    }
                }
            });
            // Title
            container.Add(new CuiElement
            {
                Parent = middleId,
                Components = {
                    new CuiTextComponent
                    {
                        Text = Lang("craft quality items", basePlayer),
                        Align = UnityEngine.TextAnchor.UpperCenter,
                        FontSize = titleTextSize,
                        Color = titleTextColor
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0.8",
                        AnchorMax = "1 1",
                        OffsetMin = $"{0} {0}",
                        OffsetMax = $"{0} {-padding}"
                    }
                }
            });
            // Image
            container.Add(new CuiElement
            {
                Parent = middleId,
                Components = {
                    new CuiImageComponent
                    {
                        Png = ImageLibrary?.Call<string>("GetImage", $"qc.button.quality"),
                        Color = "1 1 0 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.8",
                        AnchorMax = "0.5 0.8",
                        OffsetMin = $"{-imgSize/2} {-imgSize}",
                        OffsetMax = $"{imgSize/2} {0}"
                    }
                }
            });
            // Info Text
            container.Add(new CuiElement
            {
                Parent = middleId,
                Components = {
                    new CuiTextComponent
                    {
                        Text = Lang("advancing your item crafting level", basePlayer),
                        Align = UnityEngine.TextAnchor.UpperCenter,
                        FontSize = bodyTextSize,
                        Color = bodyTextColor
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 0.7",
                        OffsetMin = $"{padding} {padding}",
                        OffsetMax = $"{-padding} {-imgSize-padding}"
                    }
                }
            });
            #endregion

            // Right
            #region Right
            var rightId = $"{parent}.right";
            container.Add(new CuiElement
            {
                Parent = parent,
                Name = rightId,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = panelColor,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.667 0",
                        AnchorMax = "1 1",
                        OffsetMin = $"{halfPad} {0}",
                        OffsetMax = $"{0} {0}"
                    }
                }
            });
            // Title
            container.Add(new CuiElement
            {
                Parent = rightId,
                Components = {
                    new CuiTextComponent
                    {
                        Text = Lang("inspect your creations", basePlayer),
                        Align = UnityEngine.TextAnchor.UpperCenter,
                        FontSize = titleTextSize,
                        Color = titleTextColor
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0.8",
                        AnchorMax = "1 1",
                        OffsetMin = $"{0} {0}",
                        OffsetMax = $"{0} {-padding}"
                    }
                }
            });
            // Image
            container.Add(new CuiElement
            {
                Parent = rightId,
                Components = {
                    new CuiImageComponent
                    {
                        Png = ImageLibrary?.Call<string>("GetImage", $"qc.button.inspect"),
                        Color = "0 0.9 0.5 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.8",
                        AnchorMax = "0.5 0.8",
                        OffsetMin = $"{-imgSize/2} {-imgSize}",
                        OffsetMax = $"{imgSize/2} {0}"
                    }
                }
            });
            // Info Text
            container.Add(new CuiElement
            {
                Parent = rightId,
                Components = {
                    new CuiTextComponent
                    {
                        Text = Lang("use the quality and inspect buttons", basePlayer),
                        Align = UnityEngine.TextAnchor.UpperCenter,
                        FontSize = bodyTextSize,
                        Color = bodyTextColor
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 0.7",
                        OffsetMin = $"{padding} {padding}",
                        OffsetMax = $"{-padding} {-imgSize-padding}"
                    }
                }
            });

            return container;
            #endregion
        }

        private CuiElementContainer CreateCommandsHelpTab(BasePlayer basePlayer, CuiElementContainer container, string parent, int padding)
        {
            var entryH = 25;
            var textColor = config.Colors.Text;
            var textSize = 10;
            var top = 0;
            var gap = 4;
            var i = 0;
            bool isAdmin = basePlayer.IsAdmin || permission.UserHasPermission(basePlayer.UserIDString, PermissionAdmin);
            int lists = isAdmin ? 2 : 1;
            for (int j = 0; j < lists; j++)
            {
                List<CommandInfo> commands;
                string titleText;
                if (j == 0)
                {
                    commands = Commands.Where(x => !x.AdminOnly).ToList();
                    titleText = Lang("general commands", basePlayer);
                }
                else
                {
                    top -= entryH;
                    commands = Commands.Where(x => x.AdminOnly).ToList();
                    titleText = Lang("admin commands", basePlayer);
                }
                container.Add(new CuiElement
                {
                    Parent = parent,
                    Components =
                        {
                            new CuiTextComponent
                            {
                                Text = titleText,
                                Color = textColor,
                                FontSize = 12
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 1",
                                AnchorMax = "1 1",
                                OffsetMin = $"{0} {top-entryH}",
                                OffsetMax = $"{0} {top}"
                            }
                        }
                });
                top -= entryH;
                foreach (var command in commands.OrderBy(x => x.Rank).ThenBy(x => x.Command))
                {
                    var entryId = $"/{parent}.command.{i}";
                    var color = textColor;
                    container.Add(new CuiElement
                    {
                        Parent = parent,
                        Name = entryId,
                        Components =
                        {
                            new CuiImageComponent
                            {
                                Color = "0 0 0 0.5",
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.02 1",
                                AnchorMax = "1 1",
                                OffsetMin = $"{0} {top-entryH}",
                                OffsetMax = $"{0} {top}"
                            }
                        }
                    });
                    container.Add(new CuiElement
                    {
                        Parent = entryId,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = $"/{PREFIX} {command.Command} {command.ArgString}",
                                Align = UnityEngine.TextAnchor.MiddleLeft,
                                FontSize = textSize,
                                Color = color
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1",
                                OffsetMin = $"{padding} {0}",
                                OffsetMax = $"{0} {0}"
                            }
                        }
                    });
                    container.Add(new CuiElement
                    {
                        Parent = entryId,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = $"{command.Description}",
                                Align = UnityEngine.TextAnchor.MiddleLeft,
                                FontSize = textSize,
                                Color = textColor
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5 0",
                                AnchorMax = "1 1",
                                OffsetMin = $"{0} {0}",
                                OffsetMax = $"{0} {0}"
                            }
                        }
                    });
                    top -= (entryH + gap);
                    i++;
                }
            }

            return container;
        }

        private CuiElementContainer CreateSupportTab(BasePlayer basePlayer, CuiElementContainer container, string parent, int padding)
        {
            var textColor = config.Colors.Text;
            var titleColor = "1 1 1 1";
            // Title
            container.Add(new CuiElement
            {
                Parent = parent,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "Quality\nCrafting",
                        Color = titleColor,
                        Align = UnityEngine.TextAnchor.UpperCenter,
                        FontSize = 42
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 0.9"
                    }
                }
            });
            // Signature
            container.Add(new CuiElement
            {
                Parent = parent,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "by mr01sam",
                        Color = textColor,
                        Align = UnityEngine.TextAnchor.UpperCenter,
                        FontSize = 12
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 0.65",
                    }
                }
            });

            // Plugin Page
            var pluginPageUrl = "umod.org/plugins/quality-crafting";
            container.Add(new CuiElement
            {
                Parent = parent,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = $"<size=18>{Lang("plugin page", basePlayer)}</size>\n{pluginPageUrl}",
                        Color = textColor,
                        Align = UnityEngine.TextAnchor.UpperCenter,
                        FontSize = 14
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 0.4",
                        OffsetMin = $"{0} {0}",
                        OffsetMax = $"{0} {0}"
                    }
                }
            });

            // Donate
            var donateUrl = "ko-fi.com/mr01sam";
            container.Add(new CuiElement
            {
                Parent = parent,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = $"<size=18>{Lang("donate", basePlayer)}</size>\n{donateUrl}",
                        Color = textColor,
                        Align = UnityEngine.TextAnchor.UpperCenter,
                        FontSize = 14
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 0.25",
                        OffsetMin = $"{0} {0}",
                        OffsetMax = $"{0} {0}"
                    }
                }
            });

            return container;
        }
    }
}

namespace Oxide.Plugins
{
    partial class QualityCrafting : CovalencePlugin
    {
        #region Data
        private enum OverlayType
        {
            Inspector,
            Quality
        }

        private Dictionary<string, bool> InspectorToggled = new Dictionary<string, bool>();

        private Dictionary<string, bool> QualityToggled = new Dictionary<string, bool>();

        private Dictionary<string, Vector3> PreviousMouseCoords = new Dictionary<string, Vector3>();

        private Dictionary<string, LootedEntity> PreviousLootedEntity = new Dictionary<string, LootedEntity>();

        public bool IsInspectorToggled(BasePlayer basePlayer)
        {
            bool value = false;
            InspectorToggled.TryGetValue(basePlayer.UserIDString, out value);
            return value;
        }

        public bool IsQualityToggled(BasePlayer basePlayer)
        {
            bool value = false;
            QualityToggled.TryGetValue(basePlayer.UserIDString, out value);
            return value;
        }
        #endregion

        #region Commands

        [Command("show.inspector")]
        private void CmdInspector(IPlayer player, string command, string[] args)
        {
            var check = new SecurityCheck(args);
            if (!check.Success) { return; } else { args = check.Args; }
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer != null)
            {
                ShowInspectorButton(basePlayer);
            }
        }

        [Command("close.inspector")]
        private void CmdInspectorClose(IPlayer player, string command, string[] args)
        {
            var check = new SecurityCheck(args);
            if (!check.Success) { return; } else { args = check.Args; }
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer != null)
            {
                DestroyInspectorButton(basePlayer);
            }
        }

        [Command("show.quality")]
        private void CmdQuality(IPlayer player, string command, string[] args)
        {
            var check = new SecurityCheck(args);
            if (!check.Success) { return; } else { args = check.Args; }
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer != null)
            {
                ShowQualityButton(basePlayer);
            }
        }

        [Command("close.quality")]
        private void CmdQualityClose(IPlayer player, string command, string[] args)
        {
            var check = new SecurityCheck(args);
            if (!check.Success) { return; } else { args = check.Args; }
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer != null)
            {
                DestroyQualityButton(basePlayer);
            }
        }

        [Command("qc.inspect.item")] // qc.inspect.item <entityId> <itemUid> 
        private void CmdInspectItem(IPlayer player, string command, string[] args)
        {
            var check = new SecurityCheck(args);
            if (!check.Success) { return; } else { args = check.Args; }
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer != null)
            {
                ulong entityId = ulong.Parse(args[0]);
                BaseEntity entity = BaseNetworkable.serverEntities.Find(new NetworkableId(entityId)) as BaseEntity;
                if (entity != null)
                {
                    Item item = null;
                    var itemId = new ItemId(ulong.Parse(args[1]));
                    if (entity is PlayerCorpse)
                    {
                        var corpse = (PlayerCorpse)entity;
                        foreach (var cont in corpse.containers)
                        {
                            var found = cont.FindItemByUID(itemId);
                            if (found != null)
                            {
                                item = found;
                                break;
                            }
                        }
                    }
                    else if (entity is BasePlayer)
                    {
                        var bp = (BasePlayer)entity;
                        item = bp.inventory.FindItemUID(itemId);
                    }
                    else if (entity is DroppedItemContainer)
                    {
                        var dic = (DroppedItemContainer)entity;
                        item = dic.inventory.FindItemByUID(itemId);
                    }
                    else
                    {
                        var inventory = ((IItemContainerEntity)entity).inventory;
                        if (inventory != null)
                        {
                            item = inventory.FindItemByUID(itemId);
                        }
                    }
                    if (item != null)
                    {
                        ShowInfoOverlay(basePlayer, item);
                    }
                }
            }
        }

        [Command("qc.inspect.item.close")]
        private void CmdInspectItemClose(IPlayer player, string command, string[] args)
        {
            var check = new SecurityCheck(args);
            if (!check.Success) { return; } else { args = check.Args; }
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer != null)
            {
                CuiHelper.DestroyUi(basePlayer, INFO_OVERLAY);
            }
        }

        [Command("qc.toggle.inspector")]
        private void CmdInspectInventory(IPlayer player, string command, string[] args)
        {
            var check = new SecurityCheck(args);
            if (!check.Success) { return; } else { args = check.Args; }
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer != null)
            {
                ToggleInspectorOverlay(basePlayer);
            }
        }

        [Command("qc.toggle.quality")]
        private void CmdShowQuality(IPlayer player, string command, string[] args)
        {
            var check = new SecurityCheck(args);
            if (!check.Success) { return; } else { args = check.Args; }
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer != null)
            {
                ToggleQualityOverlay(basePlayer);
            }
        }
        #endregion

        #region Button Control
        private string INSPECTOR_BUTTON = "qc.button.inspector";
        private string QUALITY_BUTTON = "qc.button.quality";

        private void ShowInspectorButton(BasePlayer basePlayer)
        {
            var id = INSPECTOR_BUTTON;
            var container = new CuiElementContainer();
            int startX = config.HUD.InspectButton.X;
            int startY = config.HUD.InspectButton.Y;
            int size = config.HUD.InspectButton.Size;
            container.Add(new CuiElement
            {
                Name = id,
                Parent = "Overlay",
                Components =
                {
                    new CuiImageComponent
                    {
                        Png = ImageLibrary?.Call<string>("GetImage", "qc.button.inspect"),
                        Color = IsInspectorToggled(basePlayer) ? config.Colors.HUDButtonToggled : config.Colors.HUDButtonUntoggled
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = AnchorDefault,
                        AnchorMax = AnchorDefault,
                        OffsetMin = Offset(startX-GlobalOffset, startY),
                        OffsetMax = Offset(startX+size-GlobalOffset, startY+size),
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = id,
                Components = {
                    new CuiButtonComponent
                    {
                        Command = $"qc.toggle.inspector {Secret}",
                        Color = "0 0 0 0"
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }
                }
            });
            DestroyInspectorButton(basePlayer);
            CuiHelper.AddUi(basePlayer, container);
        }

        private void DestroyInspectorButton(BasePlayer basePlayer)
        {
            CuiHelper.DestroyUi(basePlayer, INSPECTOR_BUTTON);
        }

        private void ShowQualityButton(BasePlayer basePlayer)
        {
            var id = QUALITY_BUTTON;
            var container = new CuiElementContainer();
            int startX = config.HUD.QualityButton.X;
            int startY = config.HUD.QualityButton.Y;
            int size = config.HUD.QualityButton.Size;
            container.Add(new CuiElement
            {
                Name = id,
                Parent = "Overlay",
                Components =
                {
                    new CuiImageComponent
                    {
                        Png = ImageLibrary?.Call<string>("GetImage", "qc.button.quality"),
                        Color = IsQualityToggled(basePlayer) ? config.Colors.HUDButtonToggled : config.Colors.HUDButtonUntoggled
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = AnchorDefault,
                        AnchorMax = AnchorDefault,
                        OffsetMin = Offset(startX-GlobalOffset, startY),
                        OffsetMax = Offset(startX+size-GlobalOffset, startY+size),
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = id,
                Components = {
                    new CuiButtonComponent
                    {
                        Command = $"qc.toggle.quality {Secret}",
                        Color = "0 0 0 0"
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }
                }
            });
            DestroyQualityButton(basePlayer);
            CuiHelper.AddUi(basePlayer, container);
        }

        private void DestroyQualityButton(BasePlayer basePlayer)
        {
            CuiHelper.DestroyUi(basePlayer, QUALITY_BUTTON);
        }

        private void DestroyAllButtons(BasePlayer basePlayer)
        {
            DestroyInspectorButton(basePlayer);
            DestroyQualityButton(basePlayer);
            DestroySkillsMenuButton(basePlayer);
        }

        private void ShowAllButtons(BasePlayer basePlayer)
        {
            ShowInspectorButton(basePlayer);
            ShowQualityButton(basePlayer);
            ShowSkillsMenuButton(basePlayer);
        }
        #endregion

        #region Overlay IDS
        readonly string QUALIY_ID = "quality";
        readonly string INSPECTOR_ID = "inspector";
        readonly string INVENTORY_OVERLAY = "qc.overlay.{0}.inventory";
        readonly string BELT_OVERLAY = "qc.overlay.{0}.belt";
        readonly string WEAR_OVERLAY = "qc.overlay.{0}.wear";
        readonly string STORAGE_OVERLAY = "qc.overlay.{0}.storage";
        readonly string LOCKER_OVERLAY = "qc.overlay.{0}.locker";
        readonly string SHOP_OVERLAY = "qc.overlay.{0}.shop";
        readonly string INFO_OVERLAY = "qc.overlay.info";
        readonly string MODIFIER_OVERLAY = "qc.overlay.modifier";
        readonly string SCREEN_OVERLAY = "qc.overlay.screen";
        readonly string SELECTED_OVERLAY = "qc.overlay.selected";
        readonly string NPC_OVERLAY_MAIN = "qc.overlay.{0}.npc.main";
        readonly string NPC_OVERLAY_WEAR = "qc.overlay.{0}.npc.wear";
        readonly string NPC_OVERLAY_BELT = "qc.overlay.{0}.npc.belt";
        #endregion

        #region General Overlays
        private void ShowOverlayByType(BasePlayer basePlayer, OverlayType overlayType, string id)
        {
            var container = new CuiElementContainer();
            if (overlayType == OverlayType.Quality)
            {
                container = CreateModifiersOverlay(container, basePlayer);
            }
            container = CreateInventoryOverlay(basePlayer.inventory.containerMain, container, string.Format(INVENTORY_OVERLAY, id), 4, 6, 60, 440, 86, overlayType);
            container = CreateInventoryOverlay(basePlayer.inventory.containerBelt, container, string.Format(BELT_OVERLAY, id), 1, 6, 60, 440, 18, overlayType);
            container = CreateInventoryOverlay(basePlayer.inventory.containerWear, container, string.Format(WEAR_OVERLAY, id), 1, 7, 50, 52, 115, overlayType);
            if (PreviousLootedEntity.ContainsKey(basePlayer.UserIDString))
            {
                var looted = PreviousLootedEntity[basePlayer.UserIDString];
                var entity = looted.Entity;
                if (entity is VendingMachine)
                {
                    var machine = (VendingMachine)entity;
                    if (looted.IsShopMenu)
                    {
                        container = CreateShopOverlay(machine, container, string.Format(SHOP_OVERLAY, id), overlayType);
                    }
                    else
                    {
                        container = CreateInventoryOverlay(machine.inventory, container, string.Format(STORAGE_OVERLAY, id), 5, 6, 58, 838, 110, overlayType);
                    }
                }
                else if (entity is Locker)
                {
                    var locker = (Locker)entity;
                    CreateLockerOverlay(locker, container, string.Format(LOCKER_OVERLAY, id), overlayType);
                }
                else if (entity is BoxStorage)
                {
                    var box = (BoxStorage)entity;
                    int rows = 1;
                    int cols = 6;
                    if (box.inventorySlots == 18) // small box
                    {
                        rows = 3;
                    }
                    else if (box.inventorySlots == 48) // large box
                    {
                        rows = 8;
                    }
                    container = CreateInventoryOverlay(box.inventory, container, string.Format(STORAGE_OVERLAY, id), rows, cols, 58, 838, 110, overlayType);
                }
                else if (entity is StashContainer)
                {
                    var stash = (StashContainer)entity;
                    int rows = 1;
                    int cols = 6;
                    container = CreateInventoryOverlay(stash.inventory, container, string.Format(STORAGE_OVERLAY, id), rows, cols, 58, 838, 110, overlayType);
                }
                else if (entity is DroppedItemContainer)
                {
                    var dropped = (DroppedItemContainer)entity;
                    container = CreateInventoryOverlay(dropped.inventory, container, string.Format(STORAGE_OVERLAY, id), 6, 6, 58, 838, 110, overlayType);
                }
                else if (entity is PlayerCorpse)
                {
                    var corpse = (PlayerCorpse)entity;
                    var netId = corpse.net.ID;
                    int i = 0;
                    foreach(var cont in corpse.containers)
                    {
                        if (i == 0)
                        {
                            CreateInventoryOverlay(cont, container, string.Format(NPC_OVERLAY_MAIN, id), 4, 6, 58, 838, 282, overlayType, netId: netId);
                        }
                        if (i == 1)
                        {
                            CreateInventoryOverlay(cont, container, string.Format(NPC_OVERLAY_WEAR, id), 1, 6, 48, 842, 201, overlayType, netId: netId);
                        }
                        if (i == 2)
                        {
                            CreateInventoryOverlay(cont, container, string.Format(NPC_OVERLAY_BELT, id), 1, 6, 58, 838, 110, overlayType, netId: netId);
                        }
                        i++;
                    }
                }
            }
            DestroyOverlayByType(basePlayer, overlayType);
            CuiHelper.AddUi(basePlayer, container);
        }
        private void DestroyOverlayByType(BasePlayer basePlayer, OverlayType overlayType)
        {
            if (overlayType == OverlayType.Inspector)
            {
                DestroyInspectorOverlays(basePlayer);
            }
            if (overlayType == OverlayType.Quality)
            {
                DestroyQualityOverlays(basePlayer);
            }
        }
        private void DestroyAllOverlays(BasePlayer basePlayer)
        {
            DestroyInspectorOverlays(basePlayer);
            DestroyQualityOverlays(basePlayer);
            CuiHelper.DestroyUi(basePlayer, MODIFIER_OVERLAY);
        }
        private void RefreshOverlays(BasePlayer basePlayer)
        {
            if (IsInspectorToggled(basePlayer))
            {
                ShowInspectorOverlay(basePlayer);
            }
            else
            {
                DestroyInspectorOverlays(basePlayer);
            }
            ShowInspectorButton(basePlayer);
            if (IsQualityToggled(basePlayer))
            {
                ShowQualityOverlay(basePlayer);
            }
            else
            {
                DestroyQualityOverlays(basePlayer);
            }
            ShowQualityButton(basePlayer);
        }

        #endregion

        #region Inspector Overlay
        private void ShowInspectorOverlay(BasePlayer basePlayer)
        {
            ShowOverlayByType(basePlayer, OverlayType.Inspector, INSPECTOR_ID);
            InspectorToggled[basePlayer.UserIDString] = true;
        }
        private void ToggleInspectorOverlay(BasePlayer basePlayer)
        {
            if (IsInspectorToggled(basePlayer))
            {
                DestroyInspectorOverlays(basePlayer);
            }
            else
            {
                ShowInspectorOverlay(basePlayer);
            }
            ShowInspectorButton(basePlayer);
        }
        private void DestroyInspectorOverlays(BasePlayer basePlayer)
        {
            if (basePlayer == null || basePlayer.UserIDString == null || InspectorToggled == null)
            {
                return;
            }
            CuiHelper.DestroyUi(basePlayer, string.Format(INVENTORY_OVERLAY, INSPECTOR_ID));
            CuiHelper.DestroyUi(basePlayer, string.Format(BELT_OVERLAY, INSPECTOR_ID));
            CuiHelper.DestroyUi(basePlayer, string.Format(WEAR_OVERLAY, INSPECTOR_ID));
            CuiHelper.DestroyUi(basePlayer, string.Format(STORAGE_OVERLAY, INSPECTOR_ID));
            CuiHelper.DestroyUi(basePlayer, string.Format(LOCKER_OVERLAY, INSPECTOR_ID));
            CuiHelper.DestroyUi(basePlayer, string.Format(SHOP_OVERLAY, INSPECTOR_ID));
            CuiHelper.DestroyUi(basePlayer, string.Format(NPC_OVERLAY_MAIN, INSPECTOR_ID));
            CuiHelper.DestroyUi(basePlayer, string.Format(NPC_OVERLAY_BELT, INSPECTOR_ID));
            CuiHelper.DestroyUi(basePlayer, string.Format(NPC_OVERLAY_WEAR, INSPECTOR_ID));
            CuiHelper.DestroyUi(basePlayer, INFO_OVERLAY);
            CuiHelper.DestroyUi(basePlayer, SCREEN_OVERLAY);
            InspectorToggled[basePlayer.UserIDString] = false;
        }

        #endregion

        #region Quality Overlay
        private void ShowQualityOverlay(BasePlayer basePlayer)
        {
            ShowOverlayByType(basePlayer, OverlayType.Quality, QUALIY_ID);
            QualityToggled[basePlayer.UserIDString] = true;
        }
        private void ToggleQualityOverlay(BasePlayer basePlayer)
        {
            if (IsQualityToggled(basePlayer))
            {
                DestroyQualityOverlays(basePlayer);
            }
            else
            {
                ShowQualityOverlay(basePlayer);
            }
            ShowQualityButton(basePlayer);
        }
        private void DestroyQualityOverlays(BasePlayer basePlayer)
        {
            CuiHelper.DestroyUi(basePlayer, string.Format(INVENTORY_OVERLAY, QUALIY_ID));
            CuiHelper.DestroyUi(basePlayer, string.Format(BELT_OVERLAY, QUALIY_ID));
            CuiHelper.DestroyUi(basePlayer, string.Format(WEAR_OVERLAY, QUALIY_ID));
            CuiHelper.DestroyUi(basePlayer, string.Format(STORAGE_OVERLAY, QUALIY_ID));
            CuiHelper.DestroyUi(basePlayer, string.Format(LOCKER_OVERLAY, QUALIY_ID));
            CuiHelper.DestroyUi(basePlayer, string.Format(SHOP_OVERLAY, QUALIY_ID));
            CuiHelper.DestroyUi(basePlayer, string.Format(NPC_OVERLAY_MAIN, QUALIY_ID));
            CuiHelper.DestroyUi(basePlayer, string.Format(NPC_OVERLAY_BELT, QUALIY_ID));
            CuiHelper.DestroyUi(basePlayer, string.Format(NPC_OVERLAY_WEAR, QUALIY_ID));
            CuiHelper.DestroyUi(basePlayer, MODIFIER_OVERLAY);
            QualityToggled[basePlayer.UserIDString] = false;
        }
        #endregion

        #region Selected Overlay
        private void ShowSelectedOverlay(BasePlayer basePlayer, Item item)
        {
            int startX = 440;
            int startY = 18;
            int size = 60;
            int gap = 4;
            CuiElementContainer container = new CuiElementContainer();
            for (int i = 0; i < basePlayer.inventory.containerBelt.capacity; i++)
            {
                if (basePlayer.inventory.containerBelt.GetSlot(i)?.uid == item?.uid)
                {
                    container.Add(new CuiElement
                    {
                        Name = SELECTED_OVERLAY,
                        Parent = "Hud.Menu",
                        Components =
                        {
                            new CuiRectTransformComponent
                            {
                                AnchorMin = AnchorDefault,
                                AnchorMax = AnchorDefault,
                                OffsetMin = $"{startX + (size * i) + (gap * i)-GlobalOffset} {startY}",
                                OffsetMax = $"{startX + (size * (i+1)) + (gap * i)-GlobalOffset} {startY + size}"
                            }
                        }
                    });
                    int quality = QualityItemManager.GetItemQuality(item);
                    if (quality > 0)
                    {
                        var imgSize = size / 3;
                        container.Add(new CuiElement
                        {
                            Parent = SELECTED_OVERLAY,
                            Components =
                        {
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "1 1",
                                AnchorMax = "1 1",
                                OffsetMin = $"{-imgSize} {-imgSize}",
                                OffsetMax = $"{0} {0}"
                            },
                            new CuiImageComponent
                            {
                                Png = ImageLibrary?.Call<string>("GetImage", $"qc.star.{quality}"),
                                Color = "1 1 0 0.9"
                            }
                        }
                        });
                    }
                }
            }
            DestroySelectedOverlay(basePlayer);
            CuiHelper.AddUi(basePlayer, container);
        }

        private void DestroySelectedOverlay(BasePlayer basePlayer)
        {
            CuiHelper.DestroyUi(basePlayer, SELECTED_OVERLAY);
        }
        #endregion

        #region Overlay Helpers

        private CuiElementContainer CreateOverlaySlot(ItemContainer inventory, Item item, CuiElementContainer container, string id, int i, int x, int y, int w, OverlayType overlayType, NetworkableId? netId = null)
        {
            NetworkableId entityId = netId ?? (inventory.playerOwner != null ? inventory.playerOwner.net.ID : inventory.entityOwner.net.ID);
            bool hasCategory = item != null && SkillCategory.GetByItem(item) != null;
            if (overlayType == OverlayType.Inspector)
            {
                if (hasCategory)
                {
                    int quality = QualityItemManager.GetItemQuality(item);
                    container.Add(new CuiElement
                    {
                        Name = $"{id}.Entry.{i}",
                        Parent = id,
                        Components =
                        {
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 1",
                                AnchorMax = "0 1",
                                OffsetMin = $"{x} {y}",
                                OffsetMax = $"{x+w} {y+w}"
                            },
                            new CuiButtonComponent
                            {
                                Command = $"qc.inspect.item {Secret} {entityId} {item.uid}",
                                Color = SetOpacity(QualityItemManager.GetColorByQuality(quality), 0.4f)
                            }
                        }
                    });
                    container.Add(new CuiElement
                    {
                        Parent = $"{id}.Entry.{i}",
                        Components =
                        {
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.31 0.1",
                                AnchorMax = "0.81 0.6",
                            },
                            new CuiImageComponent
                            {
                                Png = ImageLibrary?.Call<string>("GetImage", $"qc.inspect"),
                            }
                        }
                    });
                }
            }
            else if (overlayType == OverlayType.Quality)
            {
                if (hasCategory)
                {
                    int quality = QualityItemManager.GetItemQuality(item);
                    int imgSize = (w / 3);
                    if (quality > 0)
                    {
                        container.Add(new CuiElement
                        {
                            Name = $"{id}.Entry.{i}.Quality",
                            Parent = id,
                            Components =
                            {
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 1",
                                    AnchorMax = "0 1",
                                    OffsetMin = $"{x+w-imgSize} {y+w-imgSize}",
                                    OffsetMax = $"{x+w} {y+w}"
                                },
                                new CuiImageComponent
                                {
                                    Png = ImageLibrary?.Call<string>("GetImage", $"qc.star.{quality}"),
                                    Color = "1 1 0 0.9"
                                }
                            }
                        }) ;
                    }
                }
            }
            return container;
        }

        private CuiElementContainer CreateInventoryOverlay(ItemContainer inventory, CuiElementContainer container, string id, int rows, int cols, int size, int startX, int startY, OverlayType overlayType, int index = 0, string layer = "Overlay", NetworkableId? netId = null)
        {
            if (inventory == null)
            {
                PrintError($"Inspector errored {id}");
                return container;
            }
            int gap = 4;
            int top = startY + (size * rows + gap * (rows-1));
            container.Add(new CuiElement
            {
                Name = id,
                Parent = layer,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "1 0 0 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = AnchorDefault,
                        AnchorMax = AnchorDefault,
                        OffsetMin = $"{startX-GlobalOffset} {top}",
                        OffsetMax = $"{startX-GlobalOffset} {top}"
                    }
                }
            });
            int w = size;
            int x = 0;
            int y = -w;
            int row = 0;
            int col = 0;
            for (int i = 0; i < rows * cols; i++)
            {
                var slot = inventory.GetSlot(i + index);
                Item item = null;
                if (slot != null)
                {
                    item = slot.FindItem(slot.uid);
                }
                container = CreateOverlaySlot(inventory, item, container, id, i, x, y, w, overlayType);
                col++;
                if (col >= cols)
                {
                    row++;
                    col = 0;
                    x = 0;
                    y -= gap + w;
                }
                else
                {
                    x += gap + w;
                }
            }
            return container;
        }

        private CuiElementContainer CreateLockerOverlay(Locker locker, CuiElementContainer container, string id, OverlayType overlayType)
        {
            int startX = 835;
            int endX = 1210;
            int startY = 136;
            int endY = 520;
            int size = 50;
            container.Add(new CuiElement
            {
                Name = id,
                Parent = "Overlay",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = AnchorDefault,
                        AnchorMax = AnchorDefault,
                        OffsetMin = $"{startX-GlobalOffset} {startY}",
                        OffsetMax = $"{endX-GlobalOffset} {endY}"
                    }
                }
            });
            int gap = 86;
            var y = -size;
            for (int k = 0; k < 3; k++)
            {
                var x = 0;
                var w = size;
                var g = 4;
                for (int i = 0; i < 13; i++)
                {
                    var idx = (k * 13) + i;
                    var item = locker.inventory.GetSlot(idx);
                    container = CreateOverlaySlot(locker.inventory, item, container, id, idx, x, y, w, overlayType);
                    x += size + g;
                    if (i == 6)
                    {
                        x = 0;
                        y -= size + g;
                    }
                }
                y -= gap;
            }
            return container;
        }

        private CuiElementContainer CreateShopOverlay(VendingMachine machine, CuiElementContainer container, string id, OverlayType overlayType)
        {
            int startX = 850;
            int startY = 114;
            int size = 60;
            int gap = 14;
            int count = machine.sellOrders.sellOrders.Count;
            container.Add(new CuiElement
            {
                Name = id,
                Parent = "Overlay",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "1 1 1 0.1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = AnchorDefault,
                        AnchorMax = AnchorDefault,
                        OffsetMin = $"{startX-GlobalOffset} {startY}",
                        OffsetMax = $"{startX+size-GlobalOffset} {startY+(size*count)+(gap*(count-1))}"
                    }
                }
            });
            int i = 0;
            foreach(var so in machine.sellOrders.sellOrders)
            {
                var itemId = so.itemToSellID;
                var top = (size * count) + (gap * (count - 1));
                var item = machine.inventory.FindItemByItemID(itemId);
                container = CreateOverlaySlot(machine.inventory, item, container, id, i, 0, -size*(i+1)-gap*(i), size, overlayType);
                i++;
            }
            return container;
        }
        #endregion

        #region Other
        private void ShowInfoOverlay(BasePlayer basePlayer, Item item)
        {
            int w = 250;
            int h = 150;
            int padding = 5;
            var qualityItem = QualityItemManager.GetByItem(item);
            int quality = QualityItemManager.GetItemQuality(item);
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiElement
            {
                Name = INFO_OVERLAY,
                Parent = "Overlay",
                Components =
                {
                    new CuiButtonComponent
                    {
                        Command = $"qc.inspect.item.close {Secret}",
                        Color = "0 0 0 0.9",
                        Sprite = "assets/content/materials/highlight.png",
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }
                }
            });
            string titleId = $"{INFO_OVERLAY}.title";
            int titleG = 4;
            int titleH = 40;
            container.Add(new CuiElement
            {
                Name = titleId,
                Parent = INFO_OVERLAY,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = $"{-w/2} {h/2+titleG}",
                        OffsetMax = $"{w/2} {h/2+titleG+titleH}"
                    },
                    new CuiImageComponent
                    {
                        Color = config.Colors.MenuBackground,
                        Material = "assets/scenes/test/waterlevelterrain/watertexture.png",
                    }
                }
            });
            string titleContentId = $"{INFO_OVERLAY}.title.content";
            container.Add(new CuiElement
            {
                Name = titleContentId,
                Parent = titleId,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = $"{padding} {padding}",
                        OffsetMax = $"{-padding} {-padding}"
                    },
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0.5",
                        Material = "assets/scenes/test/waterlevelterrain/watertexture.png",
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = titleContentId,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = $"{padding} {padding}",
                        OffsetMax = $"{-padding} {-padding}"
                    },
                    new CuiTextComponent
                    {
                        Align = TextAnchor.MiddleCenter,
                        Text = Lang("item inspection", basePlayer),
                        Color = config.Colors.Text
                    }
                }
            });
            string baseId = $"{INFO_OVERLAY}.base";
            container.Add(new CuiElement
            {
                Name = baseId,
                Parent = INFO_OVERLAY,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = $"{-w/2} {-h/2}",
                        OffsetMax = $"{w/2} {h/2}"
                    },
                    new CuiImageComponent
                    {
                        Color = config.Colors.MenuBackground,
                    }
                }
            });
            string contentId = $"{INFO_OVERLAY}.content";
            container.Add(new CuiElement
            {
                Name = contentId,
                Parent = baseId,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = $"{padding} {padding}",
                        OffsetMax = $"{-padding} {-padding}"
                    },
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0.5",
                        Material = "assets/scenes/test/waterlevelterrain/watertexture.png",
                    }
                }
            });
            int starSize = 20;
            int starGap = 5;
            int totalStarWidth = (starSize * 4) + (starGap * 4);
            for (int j = 0; j < 5; j++)
            {
                var color = "0 0 0 0.8";
                if (j < quality)
                {
                    color = "1 1 0 0.8";
                }
                container.Add(new CuiElement
                {
                    Parent = contentId,
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "1 1",
                            AnchorMax = "1 1",
                            OffsetMin = $"{(-j*starSize-starSize-starGap)} {-padding-starSize}",
                            OffsetMax = $"{(-j*starSize-starGap)} {-padding}"
                        },
                        new CuiImageComponent
                        {
                            Png = ImageLibrary?.Call<string>("GetImage", "qc.star"),
                            Color = color
                        }
                    }
                });
            }
            int imgSize = 100;
            string imgBoxId = $"{contentId}.box";
            container.Add(new CuiElement
            {
                Parent = contentId,
                Name = imgBoxId,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "1 0",
                        AnchorMax = "1 0",
                        OffsetMin = $"{-padding-imgSize} {padding}",
                        OffsetMax = $"{-padding} {padding+imgSize}"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = imgBoxId,
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = ImageLibrary?.Call<string>("GetImage", item.info.shortname)
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = $"{padding} {padding}",
                        OffsetMax = $"{-padding} {-padding}"
                    }
                }
            });
            int infoW = 180;
            int txtH = 18;
            int sigH = 12;
            container.Add(new CuiElement
            {
                Parent = contentId,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = item.info.displayName.translated,
                        Color = QualityItemManager.GetColorByQuality(quality)
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = $"{padding} {-padding-txtH}",
                        OffsetMax = $"{padding+infoW} {-padding}"
                    }
                }
            });
            string infoBoxId = $"{contentId}.info";
            var created = qualityItem.HasCreator ? Lang("crafted by", basePlayer, qualityItem.CreatorDisplayName) : Lang("no creator", basePlayer);
            container.Add(new CuiElement
            {
                Parent = contentId,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = created,
                        Color = "1 1 1 0.1",
                        FontSize = 10
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = $"{padding} {-padding-txtH-sigH}",
                        OffsetMax = $"{padding+infoW} {-padding-txtH}"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = contentId,
                Name = infoBoxId,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0 1",
                        OffsetMin = $"{padding} {padding}",
                        OffsetMax = $"{padding+infoW} {-padding-txtH-sigH-padding}"
                    }
                }
            });
            int fieldH = 20;
            int labelW = 60;
            int imgW = 0;
            int imgG = 2;
            int y = 0;
            var stats = QualityItemManager.GetItemStats(item);
            
            foreach (var stat in stats)
            {
                int value = (int)Math.Round((1f + stat.PercentModified) * 100);
                var statNameLower = stat.StatName.ToLower();
                var color = "1 1 1 1";
                if (value > 100)
                {
                    color = "0.5 1 0.5 1";
                }
                else if (value < 100)
                {
                    color = "1 0.5 0.5 1";
                }
                container.Add(new CuiElement
                {
                    Parent = infoBoxId,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = $"{Lang(statNameLower, basePlayer)}:",
                            Align = TextAnchor.MiddleLeft,
                            FontSize = 12,
                            Color = config.Colors.Text
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "1 1",
                            OffsetMin = $"{padding+imgW+imgG} {-padding-fieldH-y}",
                            OffsetMax = $"{-padding} {-padding-y}"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = infoBoxId,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = $"{value}%",
                            Align = TextAnchor.MiddleLeft,
                            FontSize = 12,
                            Color = color
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "1 1",
                            OffsetMin = $"{padding+labelW+imgW+imgG} {-padding-fieldH-y}",
                            OffsetMax = $"{-padding} {-padding-y}"
                        }
                    }
                });
                y += fieldH;
            }
            CuiHelper.DestroyUi(basePlayer, INFO_OVERLAY);
            CuiHelper.AddUi(basePlayer, container);
        }

        private void RemovedLootedEntity(BasePlayer basePlayer)
        {
            if (basePlayer != null)
            {
                PreviousLootedEntity.Remove(basePlayer.UserIDString);
                RefreshOverlays(basePlayer);
            }
        }

        private CuiElementContainer CreateModifiersOverlay(CuiElementContainer container, BasePlayer basePlayer)
        {
            var protection = basePlayer.baseProtection;
            int startX = 250;
            int startY = 226;
            int width = 100;
            int height = 405;
            int spacer = 169;
            string color = "0 1 0 0.8";
            container.Add(new CuiElement
            {
                Name = MODIFIER_OVERLAY,
                Parent = "Overlay",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "1 0 0 0"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0 0",
                        OffsetMin = $"{startX} {startY}",
                        OffsetMax = $"{startX + width} {startY + height}"
                    }
                }
            });

            int y = 0;
            int h = 41;
            int i = 0;
            foreach (Rust.DamageType dt in new Rust.DamageType[] { Rust.DamageType.Bite, Rust.DamageType.RadiationExposure, Rust.DamageType.ColdExposure, Rust.DamageType.Explosion})
            {
                var baseValue = QualityItemManager.GetClothingStatResistance(basePlayer, dt, true);
                var modValue = QualityItemManager.GetClothingStatResistance(basePlayer, dt);
                var value = modValue - baseValue;
                if (value > 0)
                {
                    container.Add(new CuiElement
                    {
                        Name = $"{MODIFIER_OVERLAY}.{i}",
                        Parent = MODIFIER_OVERLAY,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Color = color,
                                Text = $"+{(int)Math.Round(value*100)}%",
                                FontSize = 12,
                                Align = TextAnchor.LowerRight

                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 0",
                                OffsetMin = $"{0} {y}",
                                OffsetMax = $"{0} {y+h}"
                            }
                        }
                    });
                    i++;
                }
                y += h;
            }
            y += spacer;
            foreach (Rust.DamageType dt in new Rust.DamageType[] { Rust.DamageType.Slash, Rust.DamageType.Bullet })
            {
                var baseValue = QualityItemManager.GetClothingStatResistance(basePlayer, dt, true);
                var modValue = QualityItemManager.GetClothingStatResistance(basePlayer, dt);
                var value = modValue - baseValue;
                if (value > 0)
                {
                    container.Add(new CuiElement
                    {
                        Name = $"{MODIFIER_OVERLAY}.{i}",
                        Parent = MODIFIER_OVERLAY,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Color = color,
                                Text = $"+{(int)Math.Round(value*100)}%",
                                FontSize = 12,
                                Align = TextAnchor.LowerRight

                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 0",
                                OffsetMin = $"{0} {y}",
                                OffsetMax = $"{0} {y+h}"
                            }
                        }
                    });
                    i++;
                }
                y += h;
            }
            return container;
        }

        private class LootedEntity
        {
            public BaseEntity Entity;
            public bool IsShopMenu = false;
        }
        #endregion

        #region Hooks
        void OnPlayerInput(BasePlayer basePlayer, InputState input)
        {
            if (input == null || basePlayer.IsDead() || basePlayer.IsSleeping()) return;
            if (!IsInspectorToggled(basePlayer) && !IsQualityToggled(basePlayer) && !IsMenuToggled(basePlayer))
            {
                PreviousMouseCoords[basePlayer.UserIDString] = input.MouseDelta();
            }
            else
            {
                if (input.MouseDelta() != PreviousMouseCoords[basePlayer.UserIDString])
                {
                    DestroyAllMenus(basePlayer);
                    DestroyAllOverlays(basePlayer);
                    ShowQualityButton(basePlayer);
                    ShowInspectorButton(basePlayer);
                }
            }
        }

        void OnActiveItemChanged(BasePlayer basePlayer, Item oldItem, Item newItem)
        {
            if (newItem == null)
            {
                DestroySelectedOverlay(basePlayer);
                return;
            }
            ShowSelectedOverlay(basePlayer, newItem);
            if (!QualityItemManager.IsQualityItem(newItem))
            {
                return;
            }
            var heldEntity = newItem.GetHeldEntity();
            if (heldEntity == null)
            {
                return;
            }
            QualityItemManager.AuditQualityItem(ref newItem, heldEntity);
        }

        void OnLootEntity(BasePlayer player, PlayerCorpse entity)
        {
            if (player != null && entity != null)
            {
                PreviousLootedEntity[player.UserIDString] = new LootedEntity { Entity = entity };
            }
        }

        void OnLootEntity(BasePlayer player, StashContainer entity)
        {
            if (player != null && entity != null)
            {
                PreviousLootedEntity[player.UserIDString] = new LootedEntity { Entity = entity };
            }
        }

        void OnLootEntity(BasePlayer player, Locker entity)
        {
            if (player != null && entity != null)
            {
                PreviousLootedEntity[player.UserIDString] = new LootedEntity { Entity = entity };
            }
        }

        void OnLootEntity(BasePlayer player, BoxStorage entity)
        {
            if (player != null && entity != null)
            {
                PreviousLootedEntity[player.UserIDString] = new LootedEntity { Entity = entity };
            }
        }

        void OnLootEntity(BasePlayer player, DroppedItemContainer entity)
        {
            if (player != null && entity != null)
            {
                PreviousLootedEntity[player.UserIDString] = new LootedEntity { Entity = entity };
            }
        }

        void OnVendingShopOpened(VendingMachine machine, BasePlayer player)
        {
            if (player != null && machine != null)
            {
                PreviousLootedEntity[player.UserIDString] = new LootedEntity { Entity = machine, IsShopMenu = true };
            }
        }

        void OnLootEntity(BasePlayer player, VendingMachine machine)
        {
            if (player != null && machine != null)
            {
                PreviousLootedEntity[player.UserIDString] = new LootedEntity { Entity = machine }; ;
            }
        }

        void OnLootEntityEnd(BasePlayer player, PlayerCorpse entity)
        {
            RemovedLootedEntity(player);
        }

        void OnLootEntityEnd(BasePlayer player, StashContainer entity)
        {
            RemovedLootedEntity(player);
        }

        void OnLootEntityEnd(BasePlayer player, Locker entity)
        {
            RemovedLootedEntity(player);
        }

        void OnLootEntityEnd(BasePlayer player, BoxStorage entity)
        {
            RemovedLootedEntity(player);
        }

        void OnLootEntityEnd(BasePlayer player, VendingMachine entity)
        {
            RemovedLootedEntity(player);
        }

        void OnLootEntityEnd(BasePlayer player, DroppedItemContainer entity)
        {
            RemovedLootedEntity(player);
        }

        void OnLootNetworkUpdate(PlayerLoot loot)
        {
            try
            {
                if (loot != null)
                {
                    BasePlayer basePlayer = loot.baseEntity;
                    if (basePlayer != null)
                    {
                        RefreshOverlays(basePlayer);
                    }
                }
            }
            catch (Exception) { };
        }

        void OnInventoryNetworkUpdate(PlayerInventory inventory, ItemContainer container, ProtoBuf.UpdateItemContainer updateItemContainer, PlayerInventory.Type type, bool broadcast)
        {
            try
            {
                if (inventory != null && ServerInitialized)
                {
                    BasePlayer basePlayer = inventory.loot?.baseEntity;
                    if (basePlayer != null)
                    {
                        RefreshOverlays(basePlayer);
                    }
                }
            }
            catch (Exception) { };
        }
        #endregion

    }
}

namespace Oxide.Plugins
{
    partial class QualityCrafting : CovalencePlugin
    {
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["gunsmithing"] = "Gunsmithing",
                ["toolcrafting"] = "Toolcrafting",
                ["weaponsmithing"] = "Weaponsmithing",
                ["bowmaking"] = "Bowmaking",
                ["tailoring"] = "Tailoring",
                ["top crafters"] = "Top Crafters",
                ["craft speed"] = "Crafting Speed",
                ["total xp"] = "Total XP",
                ["xp"] = "XP",
                ["perks"] = "Perks",
                ["duplicate chance"] = "Duplicate Chance",
                ["no items have been crafted"] = "No items have been crafted in this category yet.",
                ["crafting skills"] = "Crafting Skills",
                ["item inspection"] = "Item Inspection",
                ["crafted by"] = "Crafted by {0}",
                ["no creator"] = "No Creator",
                ["damage"] = "Damage",
                ["durability"] = "Durability",
                ["gathering"] = "Gathering",
                ["protection"] = "Protection",
                ["gain item xp"] = "+{0} XP",
                ["gain item xp with bonus"] = "+{0} XP (+{1} Bonus)",
                ["quality chance"] = "Quality chances",
                ["player not found"] = "Player '{0}' does not exist.",
                ["command success"] = "Successfully executed command.",
                ["command error"] = "Error executing command",
                ["usage"] = "Usage: {0}",
                ["invalid value"] = "Invalid value '{0}'.",
                ["value not allowed"] = "Value '{0}' is not allowed. Allowed Values {1}.",
                ["level up skills"] = "Level Up Skills",
                ["craft items or study"] = "Craft items or study unlocked blueprints to gain XP points to advance your crafting level.\n\nYou can advance your category crafting level as well as your item crafting level for a specific item.",
                ["craft quality items"] = "Craft Quality Items",
                ["advancing your item crafting level"] = "Advancing your item crafting level will increase the chance of crafting a quality item.\n\nThere are five tiers of quality, each tier increases the base stats for that item.",
                ["inspect your creations"] = "Inspect Your Creations",
                ["use the quality and inspect buttons"] = "Use the quality and inspect buttons near the hotbar to reveal the quality level and stats of items in your inventory.\n\nQuality level is represented by a number of stars or a specific color.",
                ["overview"] = "Overview",
                ["commands"] = "Commands",
                ["support"] = "Support",
                ["crafted"] = "Crafted",
                ["max level"] = "Max Level",
                ["duplicated"] = "Duplicated!",
                ["plugin page"] = "Plugin Page",
                ["donate"] = "Donate",
                ["general commands"] = "General Commands",
                ["admin commands"] = "Admin Commands",
                ["plugin help"] = "Plugin Help",
                ["track"] = "Track",
                ["untrack"] = "Untrack",
                ["no permission"] = "You do not have permission to use that command.",
            }, this);
        }

        private string Lang(string key, string id, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        private string Lang(string key, BasePlayer basePlayer, params object[] args) => string.Format(lang.GetMessage(key, this, basePlayer?.UserIDString), args);
    }
}

namespace Oxide.Plugins
{
    partial class QualityCrafting : CovalencePlugin
    {
        [Command("show.menu")]
        private void CmdShowMenu(IPlayer player, string command, string[] args)
        {
            var check = new SecurityCheck(args);
            if (!check.Success) { return; } else { args = check.Args; }
            var basePlayer = player.Object as BasePlayer;
            ShowSkillsMenuButton(basePlayer);
        }

        [Command("close.menu")]
        private void CmdCloseMenu(IPlayer player, string command, string[] args)
        {
            var check = new SecurityCheck(args);
            if (!check.Success) { return; } else { args = check.Args; }
            var basePlayer = player.Object as BasePlayer;
            DestroySkillsMenuButton(basePlayer);
        }

        [Command("qc.menu.show")]
        private void CmdMenuShow(IPlayer player, string command, string[] args)
        {
            var check = new SecurityCheck(args);
            if (!check.Success) { return; } else { args = check.Args; }
            var basePlayer = player.Object as BasePlayer;
            var autoHide = bool.Parse(args[0]);
            ShowSkillsMenu(basePlayer, autoHide);
        }

        [Command("qc.menu.close")]
        private void CmdMenuClose(IPlayer player, string command, string[] args)
        {
            var check = new SecurityCheck(args);
            if (!check.Success) { return; } else { args = check.Args; }
            var basePlayer = player.Object as BasePlayer;
            DestroyAllMenus(basePlayer);
        }

        [Command("qc.menu.category")]
        private void CmdMenuCategory(IPlayer player, string command, string[] args)
        {
            var check = new SecurityCheck(args);
            if (!check.Success) { return; } else { args = check.Args; }
            var basePlayer = player.Object as BasePlayer;
            int id = int.Parse(args[0]);
            int page = Math.Max(0, int.Parse(args[1]));
            var category = SkillCategory.GetByID(id);
            if (category != null)
            {
                ShowCategoryPage(basePlayer, category, page);
            }
        }

        [Command("qc.track")]
        private void CmdTrack(IPlayer player, string command, string[] args)
        {
            var check = new SecurityCheck(args);
            if (!check.Success) { return; } else { args = check.Args; }
            var basePlayer = player.Object as BasePlayer;
            int id = int.Parse(args[0]);
            int page = Math.Max(0, int.Parse(args[1]));
            var category = SkillCategory.GetByID(id);
            if (category != null)
            {
                TrackingManager.Track(basePlayer, category);
                ShowCategoryPage(basePlayer, category, page);
                ShowTrackingHud(basePlayer);
            }
        }

        [Command("qc.untrack")]
        private void CmdUntrack(IPlayer player, string command, string[] args)
        {
            var check = new SecurityCheck(args);
            if (!check.Success) { return; } else { args = check.Args; }
            var basePlayer = player.Object as BasePlayer;
            int id = int.Parse(args[0]);
            int page = Math.Max(0, int.Parse(args[1]));
            var category = SkillCategory.GetByID(id);
            if (category != null)
            {
                TrackingManager.Untrack(basePlayer);
                ShowCategoryPage(basePlayer, category, page);
                DestroyTrackingHud(basePlayer);
            }
        }

        private Dictionary<string, bool> MenuToggled = new Dictionary<string, bool>();

        private string SKILLS_MENU_OVERLAY = "qc.menuoverlay";
        private string SKILLS_MENU = "qc.menu";
        private string SKILLS_MENU_BUTTON = "qc.menu.button";
        private string SKILLS_MENU_CONTENT = "qc.menu.content";
        private string SKILLS_MENU_CATEGORY = "qc.menu.category";
        private string HELP_MENU_OVERLAY = "qc.helpoverlay";
        private string HELP_MENU = "qc.help";

        private bool IsMenuToggled(BasePlayer basePlayer)
        {
            bool value = false;
            MenuToggled.TryGetValue(basePlayer.UserIDString, out value);
            return value;
        }

        private void DestroySkillsMenuButton(BasePlayer basePlayer)
        {
            CuiHelper.DestroyUi(basePlayer, SKILLS_MENU_BUTTON);
        }

        private void ShowSkillsMenuButton(BasePlayer basePlayer)
        {
            var container = new CuiElementContainer();
            int startX = config.HUD.SkillsButton.X;
            int startY = config.HUD.SkillsButton.Y;
            int size = config.HUD.SkillsButton.Size;
            container.Add(new CuiElement
            {
                Name = SKILLS_MENU_BUTTON,
                Parent = "Overlay",
                Components =
                {
                    new CuiImageComponent
                    {
                        Png = ImageLibrary?.Call<string>("GetImage", "qc.button.skills"),
                        Color = config.Colors.HUDButtonUntoggled
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = AnchorDefault,
                        AnchorMax = AnchorDefault,
                        OffsetMin = Offset(startX-GlobalOffset, startY),
                        OffsetMax = Offset(startX+size-GlobalOffset, startY+size)
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = SKILLS_MENU_BUTTON,
                Components = {
                    new CuiButtonComponent
                    {
                        Command = $"qc.menu.show {Secret} {true}",
                        Color = "0 0 0 0"
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }
                }
            });
            CuiHelper.DestroyUi(basePlayer, SKILLS_MENU_BUTTON);
            CuiHelper.AddUi(basePlayer, container);
        }

        private void DestroyAllMenus(BasePlayer basePlayer)
        {
            DestroySkillsMenu(basePlayer);
            DestroyHelpMenu(basePlayer);
            MenuToggled[basePlayer.UserIDString] = false;
        }

        private void DestroySkillsMenu(BasePlayer basePlayer)
        {
            CuiHelper.DestroyUi(basePlayer, SKILLS_MENU_OVERLAY);
            CuiHelper.DestroyUi(basePlayer, SKILLS_MENU);
        }

        private void ShowSkillsMenu(BasePlayer basePlayer, bool autoHide = true)
        {
            CuiElementContainer container = new CuiElementContainer();
            int width = 800;
            int height = 500;
            int padding = 10;
            int header = 30;
            int closeButtonSize = 15;
            var overlay = new CuiElement
            {
                Parent = "Overlay",
                Name = SKILLS_MENU_OVERLAY,
                Components =
                {
                    new CuiButtonComponent
                    {
                        Command = $"qc.menu.close {Secret}",
                        Color = "0 0 0 0.9",
                        Sprite = "assets/content/materials/highlight.png",
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }
                }
            };
            if (!autoHide)
            {
                overlay.Components.Add(new CuiNeedsCursorComponent());
            }
            container.Add(overlay);
            container.Add(new CuiElement
            {
                Parent = "Overlay",
                Name = SKILLS_MENU,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = config.Colors.MenuBackground,
                        Material = "assets/scenes/test/waterlevelterrain/watertexture.png"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = $"{-width/2} {-height/2}",
                        OffsetMax = $"{width/2} {height/2}"
                    }
                }
            });

            #region Close Btn
            container.Add(new CuiElement
            {
                Parent = SKILLS_MENU,
                Name = $"{SKILLS_MENU}.close",
                Components = {
                    new CuiButtonComponent
                    {
                        Command = $"qc.menu.close {Secret}",
                        Color = "0 0 0 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "1 1",
                        AnchorMax = "1 1",
                        OffsetMin = $"{-padding - closeButtonSize} {-padding - closeButtonSize}",
                        OffsetMax = $"{-padding} {-padding}"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = $"{SKILLS_MENU}.close",
                Components = {
                    new CuiImageComponent
                    {
                        Png = ImageLibrary?.Call<string>("GetImage", $"qc.close"),
                        Color = config.Colors.Text
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = $"{3} {3}",
                        OffsetMax = $"{-3} {-3}"
                    }
                }
            });
            #endregion

            #region Help Btn
            container.Add(new CuiElement
            {
                Parent = SKILLS_MENU,
                Name = $"{SKILLS_MENU}.help",
                Components = {
                    new CuiButtonComponent
                    {
                        Command = $"qc.help {Secret} {autoHide}",
                        Color = "0 0 0 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "1 1",
                        AnchorMax = "1 1",
                        OffsetMin = $"{-(2*padding) - (2*closeButtonSize)} {-padding - closeButtonSize}",
                        OffsetMax = $"{-(2*padding) - closeButtonSize} {-padding}"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = $"{SKILLS_MENU}.help",
                Components = {
                    new CuiImageComponent
                    {
                        Png = ImageLibrary?.Call<string>("GetImage", $"qc.help"),
                        Color = config.Colors.Text
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = $"{3} {3}",
                        OffsetMax = $"{-3} {-3}"
                    }
                }
            });
            #endregion

            container.Add(new CuiElement
            {
                Parent = SKILLS_MENU,
                Components = {
                    new CuiTextComponent
                    {
                        Text = Lang("crafting skills", basePlayer),
                        Align = UnityEngine.TextAnchor.UpperCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "1 1",
                        OffsetMin = $"{padding+100} {-padding-header}",
                        OffsetMax = $"{-padding-100} {-padding}"
                    }
                }
            });
            int sideNavWidth = 120;
            contentWidth = width - padding - padding - sideNavWidth;
            contentHeight = height - padding - padding - header - padding;
            container = CreateSkillsMenuSideNav(basePlayer, container, header, padding, sideNavWidth);
            container = CreateSkillsMenuContent(container, header, padding, sideNavWidth + padding, width);
            DestroyAllMenus(basePlayer);
            CuiHelper.AddUi(basePlayer, container);
            var category = SkillCategory.GetDefaultCategory();
            if (category != null)
            {
                ShowCategoryPage(basePlayer, category);
            }
            MenuToggled[basePlayer.UserIDString] = true;
        }

        private int contentWidth = 0;
        private int contentHeight = 0;

        private CuiElementContainer CreateSkillsMenuSideNav(BasePlayer basePlayer, CuiElementContainer container, int header, int padding, int width)
        {
            string id = $"{SKILLS_MENU}.sidenav";
            container.Add(new CuiElement
            {
                Parent = SKILLS_MENU,
                Name = id,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0 1",
                        OffsetMin = $"{padding} {padding}",
                        OffsetMax = $"{padding + width} {-padding-header}"
                    }
                }
            });
            int gap = 4;
            int y = 0;
            int h = 20;
            int i = 0;
            foreach (var category in SkillCategory.ALL)
            {
                if (SkillCategory.IsCategoryEnabled(category))
                {
                    string entryId = $"{SKILLS_MENU}.entry.{category.ID}";
                    container.Add(new CuiElement
                    {
                        Parent = id,
                        Name = entryId,
                        Components =
                    {
                        new CuiButtonComponent
                        {
                            Color = "1 1 1 0.1",
                            Command = $"qc.menu.category {Secret} {category.ID} 0"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "1 1",
                            OffsetMin = $"{0} {y-20}",
                            OffsetMax = $"{0} {y}"
                        }
                    }
                    });
                    container.Add(new CuiElement
                    {
                        Parent = entryId,
                        Components =
                    {
                        new CuiTextComponent
                        {
                            Text = category.DisplayName(basePlayer),
                            Align = UnityEngine.TextAnchor.MiddleRight,
                            FontSize = 11,
                            Color = config.Colors.Text
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1",
                            OffsetMin = $"{0} {0}",
                            OffsetMax = $"{-5} {0}"
                        }
                    }
                    });
                    container.Add(new CuiElement
                    {
                        Parent = entryId,
                        Components =
                    {
                        new CuiImageComponent
                        {
                            Png = ImageLibrary?.Call<string>("GetImage", $"qc.category.{category.Name}")
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 1",
                            OffsetMin = $"{4} {4}",
                            OffsetMax = $"{h-4} {-4}"
                        }
                    }
                    });
                    i++;
                    y -= h + gap;
                }
            }
            return container;
        }

        private CuiElementContainer CreateSkillsMenuContent(CuiElementContainer container, int header, int padding, int left, int fullWidth)
        {
            string id = $"{SKILLS_MENU}.content";
            container.Add(new CuiElement
            {
                Parent = SKILLS_MENU,
                Name = id,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0.5",
                        Material = Styles.Material
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0 1",
                        OffsetMin = $"{left + padding} {padding}",
                        OffsetMax = $"{fullWidth-padding} {-padding-header}"
                    }
                }
            });
            return container;
        }

        private void DestroyCategoryPage(BasePlayer basePlayer)
        {
            CuiHelper.DestroyUi(basePlayer, SKILLS_MENU_CATEGORY);
        }

        private void ShowCategoryPage(BasePlayer basePlayer, SkillCategory category, int page = 0)
        {
            var skills = CraftingManager.GetSkills(basePlayer);
            CuiElementContainer container = new CuiElementContainer();
            float width = 0.75f;
            container.Add(new CuiElement
            {
                Parent = SKILLS_MENU_CONTENT,
                Name = SKILLS_MENU_CATEGORY,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = $"1 1"
                    }
                }
            });
            var itemIds = skills.GetCategoryItemIDs(category);
            int padding = 10;
            int i = 0;
            int c = 0;
            int r = 0;
            int rows = 3;
            int cols = 2;
            int x = padding;
            int y = -padding;
            int w = (int)Math.Floor(((contentWidth * width) - 2 * padding) / (cols)) - padding;
            int h = ((contentHeight - footerHeight - padding) / (rows)) - padding;
            int maxPage = (int)Math.Ceiling((float)itemIds.Count / (rows * cols));
            if (itemIds.Count == 0)
            {
                container.Add(new CuiElement
                {
                    Parent = SKILLS_MENU_CATEGORY,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = Lang("no items have been crafted", basePlayer),
                            FontSize = 22,
                            Color = "1 1 1 0.2",
                            Align = UnityEngine.TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1",
                            OffsetMin = $"{0} {footerHeight + padding}",
                            OffsetMax = $"{0} {0}"
                        }
                    }
                });
            }
            else
            {

                var items = itemIds.Select(z => ItemManager.CreateByItemID(z)).OrderByDescending(z => skills.GetXP(z)).ThenBy(z => z.info.displayName.translated).Skip(rows * cols * page).Take(rows * cols).ToList();
                foreach (var item in items)
                {
                    container = CreateSkillEntry(container, basePlayer, skills, item, i, x, y, w, h);
                    x += w + padding;
                    i++;
                    c++;
                    if (c >= cols)
                    {
                        c = 0;
                        r++;
                        x = padding;
                        y -= (h + padding);
                    }
                    if (r >= rows)
                    {
                        break;
                    }
                }
                CreateLeaderboardSide(container, basePlayer, category, width, padding);
            }
            CreateContentFooter(container, basePlayer, category, skills, padding, page, page >= maxPage-1);
            DestroyCategoryPage(basePlayer);
            CuiHelper.AddUi(basePlayer, container);
        }

        private CuiElementContainer CreateSkillEntry(CuiElementContainer container, BasePlayer basePlayer, PlayerSkillSheet skills, Item item, int i, int x, int y, int w, int h)
        {
            var entryId = $"{SKILLS_MENU_CATEGORY}.entry.{i}";
            //var item = ItemManager.CreateByItemID(itemId);
            var itemDef = item.info;
            int imgPadW = 5;
            int imgPadH = 15;
            int barPad = 5;
            int barH = 10;
            var level = skills.GetLevel(item);
            var maxLevel = level >= 100;
            if (itemDef != null)
            {
                container.Add(new CuiElement
                {
                    Parent = SKILLS_MENU_CATEGORY,
                    Name = entryId,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = "0 0 0 0.8"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "0 1",
                            OffsetMin = $"{x} {y-h}",
                            OffsetMax = $"{x+w} {y}"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = entryId,
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = ImageLibrary?.Call<string>("GetImage", itemDef.shortname)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 0",
                            OffsetMin = $"{imgPadW} {imgPadH}",
                            OffsetMax = $"{imgPadW+(h)-24} {h-imgPadH}"
                        }
                    }
                });
                if (!maxLevel)
                {
                    var barId = $"{entryId}.bar";
                    container.Add(new CuiElement
                    {
                        Name = barId,
                        Parent = entryId,
                        Components =
                        {
                            new CuiImageComponent
                            {
                                Color = "0 0 0 1"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "1 0",
                                AnchorMax = "1 0",
                                OffsetMin = $"{-barPad - (w + imgPadW - h)} {barPad}",
                                OffsetMax = $"{-barPad} {barH + barPad}"
                            }
                        }
                    });
                    container.Add(new CuiElement
                    {
                        Parent = barId,
                        Components =
                        {
                            new CuiImageComponent
                            {
                                Color = "0.5 1 0.5 1"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",
                                AnchorMax = $"{skills.GetLevelPercent(item)} 1",
                                OffsetMin = "2 2",
                                OffsetMax = "-2 -2"
                            }
                        }
                    });
                }
                
                container.Add(new CuiElement
                {
                    Parent = entryId,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = $"{level}",
                            Align = UnityEngine.TextAnchor.LowerLeft,
                            Color = config.Colors.Text,
                            FontSize = 11
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "1 0",
                            AnchorMax = "1 0",
                            OffsetMin = $"{-barPad - (w + imgPadW - h) + 4} {barPad + 12}",
                            OffsetMax = $"{-barPad + 4} {barH + barPad + 36}"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = entryId,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = $"{itemDef.displayName.translated}",
                            Align = UnityEngine.TextAnchor.LowerLeft,
                            Color = config.Colors.Text,
                            FontSize = 11
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "1 0",
                            AnchorMax = "1 0",
                            OffsetMin = $"{-barPad - (w + imgPadW - h) + 4} {barPad + 26}",
                            OffsetMax = $"{-barPad + 4} {barH + barPad + 50}"
                        }
                    }
                });

                var chances = CraftingManager.GetQualityChances(level).Where(qc => qc.Percent > 0);
                int ch = 12;
                int cw = 40;
                int cp = 4;
                int top = -cp;
                int j = 0;
                foreach (var chance in chances)
                {
                    container.Add(new CuiElement
                    {
                        Parent = entryId,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = $"L{chance.Quality}",
                                Align = UnityEngine.TextAnchor.UpperRight,
                                Color = config.Colors.Text,
                                FontSize = 8
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "1 1",
                                AnchorMax = "1 1",
                                OffsetMin = $"{-cw-cp-24} {top-ch-cp-2}",
                                OffsetMax = $"{-cp-24} {top-2}"
                            }
                        }
                    });
                    container.Add(new CuiElement
                    {
                        Parent = entryId,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = $"{chance.Percent}%",
                                Align = UnityEngine.TextAnchor.UpperRight,
                                Color = QualityItemManager.GetColorByQuality(chance.Quality),
                                FontSize = 10
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "1 1",
                                AnchorMax = "1 1",
                                OffsetMin = $"{-cw-cp} {top-ch-cp}",
                                OffsetMax = $"{-cp} {top}"
                            }
                        }
                    });
                    top -= ch + cp;
                    j++;
                }


            }
            return container;
        }

        private int footerHeight = 70;

        private CuiElementContainer CreateLeaderboardSide(CuiElementContainer container, BasePlayer basePlayer, SkillCategory category, float width, int padding)
        {
            string id = $"{SKILLS_MENU_CATEGORY}.leaderboard";
            int titleH = 30;
            container.Add(new CuiElement
            {
                Parent = SKILLS_MENU_CATEGORY,
                Name = id,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"{width} 0",
                        AnchorMax = "1 1",
                        OffsetMin = $"{0} {padding + footerHeight + padding}",
                        OffsetMax = $"{-padding} {-padding}"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = id, 
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = Lang("top crafters", basePlayer),
                        Align = UnityEngine.TextAnchor.UpperLeft,
                        Color = config.Colors.Text,
                        FontSize = 14
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"0 1",
                        AnchorMax = "1 1",
                        OffsetMin = $"{padding} {-titleH-padding}",
                        OffsetMax = $"{-padding} {-padding}"
                    }
                }
            });
            var listId = $"{id}.list";
            container.Add(new CuiElement
            {
                Parent = id,
                Name = listId,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"0 0",
                        AnchorMax = "1 1",
                        OffsetMin = $"{padding} {0}",
                        OffsetMax = $"{-padding} {-titleH-padding}"
                    }
                }
            });
            int numPerPage = 10;
            int page = 0;
            var playerRankings = CraftingManager.GetTopPlayerSkills(category, 0, numPerPage).Where(x => !string.IsNullOrEmpty(x.UserDisplayName));
            //for (int n = playerRankings.Count; n < numPerPage; n++)
            //{
            //    playerRankings.Add(new LeaderboardRank { Level = 10, UserIdString = playerRankings[0].UserIdString });
            //}
            int rowIdx = 0;
            float rowH = 0.8f / numPerPage;
            float top = 1f;
            foreach(var ranking in playerRankings)
            {
                int rank = 1 + rowIdx + (numPerPage * page);
                var rowId = $"{listId}.{rowIdx}";
                var color = (basePlayer.UserIDString == ranking.UserIdString) ? "1 1 1 1" : config.Colors.Text;
                container.Add(new CuiElement
                {
                    Parent = listId,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = $"{rank}.",
                            Align = UnityEngine.TextAnchor.MiddleLeft,
                            Color = color,
                            FontSize = 12
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = $"0 {top-rowH}",
                            AnchorMax = $"1 {top}"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = listId,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = $"{ranking.UserDisplayName} (LVL {ranking.Level})",
                            Align = UnityEngine.TextAnchor.MiddleRight,
                            Color = color,
                            FontSize = 12
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = $"0 {top-rowH}",
                            AnchorMax = $"1 {top}"
                        }
                    }
                });
                top -= rowH;
                rowIdx++;
            }
            return container;
        }

        private CuiElementContainer CreateContentFooter(CuiElementContainer container, BasePlayer basePlayer, SkillCategory category, PlayerSkillSheet skills, int padding, int page, bool maxed)
        {
            string id = $"{SKILLS_MENU_CATEGORY}.footer";
            int h = footerHeight;
            int c = 10;
            var lvl = skills.GetLevel(category);
            bool IsMaxLevel = lvl >= 100;
            container.Add(new CuiElement
            {
                Parent = SKILLS_MENU_CATEGORY,
                Name = id,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0.8"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 0",
                        OffsetMin = $"{padding} {padding}",
                        OffsetMax = $"{-padding} {padding + h}"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = id,
                Components =
                    {
                        new CuiImageComponent
                        {
                            Png = ImageLibrary?.Call<string>("GetImage", $"qc.category.{category.Name}")
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 0",
                            OffsetMin = $"{c} {c}",
                            OffsetMax = $"{h-c} {h-c}"
                        }
                    }
            });
            
            int barPad = padding;
            int barW = 200;
            int barH = 15;
            if (!IsMaxLevel)
            {
                var barId = $"{id}.bar";
                container.Add(new CuiElement
                {
                    Name = barId,
                    Parent = id,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = "0 0 0 1"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 0",
                            OffsetMin = $"{h-c+barPad} {c}",
                            OffsetMax = $"{h-c+barPad+barW} {c + barH}"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = barId,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = config.Colors.XPBar
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = $"{skills.GetLevelPercent(category)} 1",
                            OffsetMin = "2 2",
                            OffsetMax = "-2 -2"
                        }
                    }
                });
                var cumPrevLevelXp = lvl > 0 ? category.GetCategoryLevelXPReq(lvl) : 0;
                var cumCurrentXp = skills.GetXP(category);
                var cumNextLevelXp = category.GetCategoryLevelXPReq(lvl + 1);
                var relCurrentXp = cumCurrentXp - cumPrevLevelXp;
                var relNextLevelXp = cumNextLevelXp - cumPrevLevelXp;
                container.Add(new CuiElement
                {
                    Parent = id,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = $"{relCurrentXp}/{relNextLevelXp} {Lang("xp", basePlayer)}",
                            Align = UnityEngine.TextAnchor.LowerRight,
                            Color = config.Colors.Text,
                            FontSize = 10
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 0",
                            OffsetMin = $"{h-c+barPad+3} {c+barH+3}",
                            OffsetMax = $"{h-c+barPad+barW} {c+barH+42}"
                        }
                    }
                });
            }
            container.Add(new CuiElement
            {
                Parent = id,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = $"{skills.GetLevel(category)}",
                        Align = UnityEngine.TextAnchor.LowerLeft,
                        Color = config.Colors.Text,
                        FontSize = 14
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0 0",
                        OffsetMin = $"{h-c+barPad+3} {c+barH+1}",
                        OffsetMax = $"{h-c+barPad+barW+3} {c+barH+42}"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = id,
                Components =
                    {
                        new CuiTextComponent
                        {
                            Text = $"{category.DisplayName(basePlayer)}",
                            Align = UnityEngine.TextAnchor.LowerLeft,
                            Color = config.Colors.Text,
                            FontSize = 14
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 0",
                            OffsetMin = $"{h-c+barPad+2} {c+barH+18}",
                            OffsetMax = $"{h-c+barPad+barW+2} {c+barH+42}"
                        }
                    }
            });

            #region Stats
            var craftSpeed = skills.GetCraftingSpeedMultiplier(category);
            var left = h - c + barPad + barW + 60;
            var color = config.Colors.Text;
            int valueX = 80;
            int hmin = c + barH + 18;
            int hmax = c + barH + 42;
            int rowh = 15;
            int bottom = hmin - rowh;
            int leftV = 4;
            // Perks
            container.Add(new CuiElement
            {
                Parent = id,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = $"{Lang("perks", basePlayer)}:",
                        Align = UnityEngine.TextAnchor.LowerLeft,
                        Color = config.Colors.Text,
                        FontSize = 14
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0 0",
                        OffsetMin = $"{left} {c+barH+18}",
                        OffsetMax = $"{left+200} {c+barH+42}"
                    }
                }
            });
            left += leftV;
            var statSize = 10;
            // Craft Speed
            container.Add(new CuiElement
            {
                Parent = id,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = $"{Lang("craft speed", basePlayer)}:",
                        Align = UnityEngine.TextAnchor.LowerLeft,
                        Color = config.Colors.Text,
                        FontSize = statSize
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0 0",
                        OffsetMin = $"{left} {bottom}",
                        OffsetMax = $"{left + valueX} {bottom + rowh}"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = id,
                Components =
                    {
                        new CuiTextComponent
                        {
                            Text = $"{ColorTextRGB($"{(int)Math.Round((craftSpeed) * 100)}%", color)}",
                            Align = UnityEngine.TextAnchor.LowerLeft,
                            Color = config.Colors.Text,
                            FontSize = statSize
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 0",
                            OffsetMin = $"{left+valueX} {bottom}",
                            OffsetMax = $"{left+valueX+100} {bottom + rowh}"
                        }
                    }
            });
            // Duplicate Chance
            bottom -= rowh;
            container.Add(new CuiElement
            {
                Parent = id,
                Components =
                    {
                        new CuiTextComponent
                        {
                            Text = $"{Lang("duplicate chance", basePlayer)}:",
                            Align = UnityEngine.TextAnchor.LowerLeft,
                            Color = config.Colors.Text,
                            FontSize = statSize
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 0",
                            OffsetMin = $"{left} {bottom}",
                            OffsetMax = $"{left + valueX} {bottom + rowh}"
                        }
                    }
            });
            container.Add(new CuiElement
            {
                Parent = id,
                Components =
                    {
                        new CuiTextComponent
                        {
                            Text = $"{skills.GetDuplicateChance(category)}%",
                            Align = UnityEngine.TextAnchor.LowerLeft,
                            Color = config.Colors.Text,
                            FontSize = statSize
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 0",
                            OffsetMin = $"{left+valueX} {bottom}",
                            OffsetMax = $"{left+valueX+100} {bottom + rowh}"
                        }
                    }
            });
            #endregion

            #region Track Button
            var trackW = 50;
            var trackH = 20;
            bool isTracked = TrackingManager.IsTracking(basePlayer, category);
            container.Add(new CuiElement
            {
                Parent = id,
                Name = $"{id}.track",
                Components = {
                    new CuiButtonComponent
                    {
                        Command = isTracked ? $"qc.untrack {Secret} {category.ID} {page}" : $"qc.track {Secret} {category.ID} {page}",
                        Color = isTracked ? "1 0 0 1" : "0 0 0 1" 
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "1 1",
                        AnchorMax = "1 1",
                        OffsetMin = $"{-padding - trackW} {-padding - trackH}",
                        OffsetMax = $"{-padding} {-padding}"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = $"{id}.track",
                Components = {
                    new CuiTextComponent
                    {
                        Text = isTracked ? Lang("untrack", basePlayer) : Lang("track", basePlayer),
                        FontSize = 12,
                        Align = UnityEngine.TextAnchor.MiddleCenter,
                        Color = config.Colors.Text,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = $"{3} {3}",
                        OffsetMax = $"{-3} {-3}"
                    }
                }
            });
            #endregion

            int btnW = 35;
            int btnH = 20;
            int btnX = 10;
            int btnY = 6;
            var btnUpDownX = -123;
            var btnUpY = 40;
            if (page > 0)
            {
                container.Add(new CuiElement
                {
                    Parent = id,
                    Name = $"{id}.btn.up",
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Command = $"qc.menu.category {Secret} {category.ID} {page-1}",
                            Color = "0 0 0 1"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "1 1",
                            AnchorMax = "1 1",
                            OffsetMin = $"{btnUpDownX-btnW} {btnUpY}",
                            OffsetMax = $"{btnUpDownX} {btnUpY+btnH}"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = $"{id}.btn.up",
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Png = ImageLibrary?.Call<string>("GetImage", $"qc.up"),
                            Color = config.Colors.Text
                        },
                        new CuiRectTransformComponent
                        {
                            OffsetMin = $"{btnX} {btnY}",
                            OffsetMax = $"{-btnX} {-btnY}"
                        }
                    }
                });
            }
            var btnDownY = 10;
            if (!maxed)
            {
                container.Add(new CuiElement
                {
                    Parent = id,
                    Name = $"{id}.btn.down",
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Command = $"qc.menu.category {Secret} {category.ID} {page+1}",
                            Color = "0 0 0 1",
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "1 1",
                            AnchorMax = "1 1",
                            OffsetMin = $"{btnUpDownX-btnW} {btnDownY}",
                            OffsetMax = $"{btnUpDownX} {btnDownY+btnH}"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = $"{id}.btn.down",
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Png = ImageLibrary?.Call<string>("GetImage", $"qc.down"),
                            Color = config.Colors.Text
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1",
                            OffsetMin = $"{btnX} {btnY}",
                            OffsetMax = $"{-btnX} {-btnY}"
                        }
                    }
                });
            }
            return container;
        }
    }
}

namespace Oxide.Plugins
{
    partial class QualityCrafting : CovalencePlugin
    {
		object OnEntityTakeDamage(BasePlayer entity, HitInfo info)
		{
			foreach (Item item in entity.inventory.containerWear.itemList)
			{
				/* Protection increase by 1.5 */
				/* Scale by 1.5 - 1 = 0.5 */
				float modifier = 1f + QualityItemManager.GetItemQuality(item) * config.Qualities.Modifiers.Protection;
				info.damageTypes.ScaleAll(1f / modifier);
			}
			return null;
		}
	}
}

namespace Oxide.Plugins
{
    partial class QualityCrafting : CovalencePlugin
    {

        #region UI
        private static readonly string NOTIFICATION_ID = $"qc.notification";

        public void CreateProgressBar(BasePlayer basePlayer, List<CuiElement> collection, string id, string parent, string anchorMin, string anchorMax, string offsetMin, string offsetMax, float fadeOut, float fadeIn, float delay, string bgColor, string barColor, int oldLevel, int newLevel, uint xpGained, float oldPercent, float newPercent, string textColor, int textSize, bool isLevelUp, string glowElementId, float progressDelay = 0.25f, bool showXpGained = true, uint xp1 = 0, uint xp2 = 0, bool showTitle = false, string title = "")
        {
            var tracked = new List<CuiElement>();
            bool maxLevel = oldLevel >= 100;
            var transparent = "0 0 0 0";
            // Black bar
            tracked.Add(new CuiElement
            {
                Name = id,
                Parent = parent,
                FadeOut = fadeOut,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = maxLevel ? transparent : bgColor,
                        FadeIn = fadeIn,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = anchorMin,
                        AnchorMax = anchorMax,
                        OffsetMin = offsetMin,
                        OffsetMax = offsetMax
                    }
                }
            });
            // Old Bar
            var oldBarId = $"{id}.oldbar";
            var oldBarElement = new CuiElement
            {
                Name = oldBarId,
                Parent = id,
                //FadeOut = fadeOut,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = maxLevel ? transparent : barColor,
                        FadeIn = fadeIn,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = $"{oldPercent} 1",
                        OffsetMin = $"0 0",
                        OffsetMax = $"0 {-1}"
                    }
                }
            };
            tracked.Add(oldBarElement);
            int offSet = 1;
            string levelTextId = $"{id}.level";
            // Level Text
            var levelTextElement = new CuiElement
            {
                Name = levelTextId,
                Parent = id,
                //FadeOut = fadeOut,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = $"{(showTitle ? $"{title}\n" : "")}{oldLevel}",
                        FontSize = textSize,
                        Color = textColor,
                        Align = UnityEngine.TextAnchor.LowerLeft,
                        FadeIn = delay,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",
                        AnchorMax = $"0 1",
                        OffsetMin = $"0 {offSet}",
                        OffsetMax = $"200 25"
                    }
                }
            };
            tracked.Add(levelTextElement);
            // XP Text
            tracked.Add(new CuiElement
            {
                Name = $"{id}.xp",
                Parent = id,
                FadeOut = fadeOut,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = showXpGained ? $"+{xpGained} {Lang("xp", basePlayer)}" : $"{xp1}/{xp2} {Lang("xp", basePlayer)}",
                        FontSize = textSize,
                        Color = maxLevel ? transparent : textColor,
                        Align = UnityEngine.TextAnchor.LowerRight,
                        FadeIn = delay,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "1 1",
                        AnchorMax = $"1 1",
                        OffsetMin = $"-200 {offSet}",
                        OffsetMax = $"0 25"
                    }
                }
            });

            // New Bar
            var newBarId = $"{id}.newbar";
            var newBarElement = new CuiElement
            {
                Name = newBarId,
                Parent = id,
                FadeOut = fadeOut,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0",
                        FadeIn = delay,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = $"{(isLevelUp ? 1f : newPercent)} 1",
                        OffsetMin = $"0 0",
                        OffsetMax = $"0 {-1}"
                    }
                }
            };
            tracked.Add(newBarElement);
            collection.AddRange(tracked);
            PLUGIN.timer.In(progressDelay, () =>
            {
                CuiElementContainer temp = new CuiElementContainer();
                ((CuiImageComponent)newBarElement.Components[0]).Color = barColor;
                temp.Add(newBarElement);
                CuiHelper.DestroyUi(basePlayer, newBarId);
                CuiHelper.AddUi(basePlayer, temp);
            });


            // LevelUp
            if (isLevelUp)
            {
                tracked = new List<CuiElement>();
                var glowId = $"{id}.glow";
                PLUGIN.timer.In(delay + progressDelay, () =>
                {
                    PLUGIN.PlaySfx(basePlayer, PLUGIN.config.SFX.SkillLevelUp);
                    tracked.Add(new CuiElement
                    {
                        Name = glowId,
                        Parent = glowElementId,
                        FadeOut = 0.5f,
                        Components =
                        {
                            new CuiImageComponent
                            {
                                Color = "1 1 1 0.7",
                                FadeIn = 0.05f
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1"
                            }
                        }
                    });
                    // Add Tracked
                    var container = new CuiElementContainer();
                    container.AddRange(tracked);
                    CuiHelper.AddUi(basePlayer, container);
                    // Start Glow Destroy Timer
                    PLUGIN.timer.In(0.2f, () =>
                    {
                        CuiHelper.DestroyUi(basePlayer, glowId);
                    });
                    // Update
                    container = new CuiElementContainer();
                    // Update Level Text
                    CuiHelper.DestroyUi(basePlayer, levelTextId);
                    ((CuiTextComponent)levelTextElement.Components[0]).Text = $"{(showTitle ? $"{title}\n" : "")}{newLevel}";
                    levelTextElement.FadeOut = fadeOut;
                    container.Add(levelTextElement);
                    // Update Old Bar
                    CuiHelper.DestroyUi(basePlayer, oldBarId);
                    ((CuiRectTransformComponent)oldBarElement.Components[1]).AnchorMax = $"{0} 1";
                    container.Add(oldBarElement);
                    // Update New Bar
                    CuiHelper.DestroyUi(basePlayer, newBarId);
                    CuiHelper.AddUi(basePlayer, container);
                    PLUGIN.timer.In(0.6f, () =>
                    {
                        container = new CuiElementContainer();
                        ((CuiRectTransformComponent)newBarElement.Components[1]).AnchorMax = $"{newPercent} 1";
                        container.Add(newBarElement);
                        CuiHelper.AddUi(basePlayer, container);
                    });
                });
            }
        }

        private void ShowXpGainedNotification(BasePlayer basePlayer, GainedXPNotification notification)
        {
            var id = NOTIFICATION_ID;
            var container = new CuiElementContainer();
            var backgroundColor = PLUGIN.config.Colors.MenuBackground;
            var fadeOut = 0.6f;
            var fadeIn = 0.1f;
            var pause = /*config.Notifications.Pause*/ 2.85f;
            int x = /*config.Notifications.LevelUpItemNotification.X*/ 530;
            int y = PLUGIN.config.Notifications.LevelUpItemNotification.Y;
            int w = 200;
            int h = 42;
            var icon = notification.Icon;
            var padding = Styles.NotificationPadding;
            var textColor = PLUGIN.config.Colors.Text;
            //var qualityColor = QualityItemManager.GetColorByQuality(notification.Quality);
            var imgBgColor = "0 0 0 0.8";
            var barColor = PLUGIN.config.Colors.XPBar;
            var progressDelay = fadeIn + 0.75f;
            var isCategory = icon.StartsWith("qc.category");
            List<CuiElement> tracked = new List<CuiElement>();
            // Base
            tracked.Add(new CuiElement
            {
                Name = id,
                Parent = "Hud",
                FadeOut = fadeOut,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = backgroundColor,
                        FadeIn = fadeIn,
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0 0",
                        OffsetMin = $"{x} {y}",
                        OffsetMax = $"{x+w} {y+h}"
                    }
                }
            });
            // Image Background
            var imgBgId = $"{id}.img.bg";
            tracked.Add(new CuiElement
            {
                Name = imgBgId,
                Parent = id,
                FadeOut = fadeOut,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = imgBgColor,
                        FadeIn = fadeIn,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0 0",
                        OffsetMin = $"{padding} {padding}",
                        OffsetMax = $"{h-padding} {h-padding}"
                    }
                }
            });
            // Image
            var imgP = 4;
            var imgId = $"{id}.img";
            if (isCategory)
            {
                tracked.Add(new CuiElement
                {
                    Name = imgId,
                    Parent = imgBgId,
                    FadeOut = fadeOut,
                    Components =
                        {
                            new CuiImageComponent
                            {
                                Png = PLUGIN.ImageLibrary?.Call<string>("GetImage", icon),
                                FadeIn = fadeIn,
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1",
                                OffsetMin = $"{imgP} {imgP}",
                                OffsetMax = $"{-imgP} {-imgP}"
                            }
                        }
                });
            }
            else
            {
                tracked.Add(new CuiElement
                {
                    Name = imgId,
                    Parent = imgBgId,
                    FadeOut = fadeOut,
                    Components =
                        {
                            new CuiRawImageComponent
                            {
                                Png = PLUGIN.ImageLibrary?.Call<string>("GetImage", icon),
                                FadeIn = fadeIn,
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1",
                                OffsetMin = $"{1} {1}",
                                OffsetMax = $"{-1} {-1}"
                            }
                        }
                });
            }
            // XP Bar;
            var barH = 8;
            var xpImgH = h - imgP - imgP;
            var barTxtSize = 9;
            var xpBlackBar1Id = $"{id}.blackbar.1";
            CreateProgressBar(basePlayer, tracked, xpBlackBar1Id, id, "0 1", "1 1", $"{xpImgH + padding} {-xpImgH}", $"{-padding} {-xpImgH + barH}", fadeOut, fadeIn, progressDelay, imgBgColor, barColor, notification.OldLevel, notification.NewLevel, notification.XPGained, notification.OldXP, notification.NewXP, textColor, barTxtSize, notification.IsLevelUp, id, 0.25f, true, 0, 0, true, notification.SkillDisplayName);

            container.AddRange(tracked);
            CuiHelper.DestroyUi(basePlayer, id);
            CuiHelper.AddUi(basePlayer, container);
            PLUGIN.PlaySfx(basePlayer, PLUGIN.config.SFX.ItemCraftedNormal);
            PLUGIN.timer.In(pause, () =>
            {
                foreach (var item in tracked)
                {
                    CuiHelper.DestroyUi(basePlayer, item.Name);
                }
            });
        }


        private void ShowItemCraftedNotification(BasePlayer basePlayer, ItemCraftedNotification notification)
        {
            var id = NOTIFICATION_ID;
            var container = new CuiElementContainer();
            var backgroundColor = PLUGIN.config.Colors.MenuBackground;
            var fadeOut = Styles.FadeOut;
            var fadeIn = Styles.FadeIn;
            var pause = 2.85f;
            int x = PLUGIN.config.Notifications.ItemCraftedNotification.X;
            int y = PLUGIN.config.Notifications.ItemCraftedNotification.Y;
            int w = 300;
            int h = 70;
            var icon = notification.Icon;
            var padding = Styles.NotificationPadding;
            var textColor = PLUGIN.config.Colors.Text;
            var qualityColor = QualityItemManager.GetColorByQuality(notification.Quality);
            var imgBgColor = "0 0 0 0.8";
            var itemImgBgColor = imgBgColor;
            var barColor = PLUGIN.config.Colors.XPBar;
            var progressDelay = fadeIn + 0.75f;
            List<CuiElement> tracked = new List<CuiElement>();

            // Base
            tracked.Add(new CuiElement
            {
                Name = id,
                Parent = "Hud",
                FadeOut = fadeOut,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = backgroundColor,
                        FadeIn = fadeIn,
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = AnchorDefault,
                        AnchorMax = AnchorDefault,
                        OffsetMin = $"{x-GlobalOffset} {y}",
                        OffsetMax = $"{x+w-GlobalOffset} {y+h}"
                    }
                }
            });
            // Image Background
            var imgBgId = $"{id}.img.bg";
            tracked.Add(new CuiElement
            {
                Name = imgBgId,
                Parent = id,
                FadeOut = fadeOut,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = itemImgBgColor,
                        FadeIn = fadeIn,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0 0",
                        OffsetMin = $"{padding} {padding}",
                        OffsetMax = $"{h-padding} {h-padding}"
                    }
                }
            });
            // Image
            var imgP = 4;
            var imgId = $"{id}.img";
            tracked.Add(new CuiElement
            {
                Name = imgId,
                Parent = imgBgId,
                FadeOut = fadeOut,
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = PLUGIN.ImageLibrary?.Call<string>("GetImage", icon),
                        FadeIn = fadeIn,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = $"{imgP} {imgP}",
                        OffsetMax = $"{-imgP} {-imgP}"
                    }
                }
            });
            // Quality
            var imgSize = 2f / 5f;
            if (notification.Quality > 0)
            {
                tracked.Add(new CuiElement
                {
                    Name = $"{imgId}.quality",
                    Parent = imgId,
                    FadeOut = fadeOut,
                    Components =
                        {
                            new CuiImageComponent
                            {
                                Png = PLUGIN.ImageLibrary?.Call<string>("GetImage", $"qc.star.{notification.Quality}"),
                                FadeIn = fadeIn,
                                Color = "1 1 0 0.9"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = $"{1f-imgSize} {1f-imgSize}",
                                AnchorMax = $"1 1"
                            }
                        }
                });
            }
            // Text
            var textLength = 100;
            var textSize = 12;
            tracked.Add(new CuiElement
            {
                Name = $"{id}.text.1",
                Parent = id,
                FadeOut = fadeOut,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = $"{Lang("crafted", basePlayer)}\n{(ColorTextRGB(notification.SkillDisplayName, qualityColor))}",
                        Align = UnityEngine.TextAnchor.UpperLeft,
                        Color = textColor,
                        FontSize = textSize,
                        FadeIn = fadeIn,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0 0",
                        OffsetMin = $"{h} {padding}",
                        OffsetMax = $"{h+textLength} {h-padding}"
                    }
                }
            });
            // Duplicate Text
            if (notification.IsDuplicated)
            {
                var dupeId = $"{id}.dupe.text";
                tracked.Add(new CuiElement
                {
                    Name = $"{id}.dupe.text",
                    Parent = id,
                    FadeOut = fadeOut,
                    Components =
                {
                    new CuiTextComponent
                    {
                        Text = $"{Lang("duplicated", basePlayer)}",
                        Align = UnityEngine.TextAnchor.LowerLeft,
                        Color = "0.5 0.8 1 1",
                        FontSize = textSize,
                        FadeIn = fadeIn,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0 0",
                        OffsetMin = $"{h} {padding}",
                        OffsetMax = $"{h+textLength} {h-padding}"
                    }
                }
                });
            }
            // Left
            var leftStart = 0.5f;
            var leftId = $"{id}.left";
            tracked.Add(new CuiElement
            {
                Name = leftId,
                Parent = id,
                FadeOut = fadeOut,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0",
                        FadeIn = fadeIn
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"{leftStart} 0",
                        AnchorMax = "1 1",
                        OffsetMin = $"{padding} {padding}",
                        OffsetMax = $"{-padding} {-padding}"
                    }
                }
            });
            // Top
            var topId = $"{leftId}.top";
            var topH = 20;
            tracked.Add(new CuiElement
            {
                Name = topId,
                Parent = leftId,
                FadeOut = fadeOut,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0",
                        FadeIn = fadeIn
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "1 1",
                        OffsetMin = $"{0} {-topH}",
                        OffsetMax = $"{0} {0}"
                    }
                }
            });
            // XP Image BG 1;
            var xpImgH = topH;
            var xpImgP1 = 2;
            var xpImg1BgId = $"{id}.xpimg1.bg";
            tracked.Add(new CuiElement
            {
                Name = xpImg1BgId,
                Parent = topId,
                FadeOut = fadeOut,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = imgBgColor,
                        FadeIn = fadeIn,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = $"{0} {-xpImgH}",
                        OffsetMax = $"{xpImgH} {0}"
                    }
                }
            });
            // XP Image 1;
            tracked.Add(new CuiElement
            {
                Name = $"{id}.xpimg1",
                Parent = xpImg1BgId,
                FadeOut = fadeOut,
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = PLUGIN.ImageLibrary?.Call<string>("GetImage", icon),
                        FadeIn = fadeIn,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = $"{xpImgP1} {xpImgP1}",
                        OffsetMax = $"{-xpImgP1} {-xpImgP1}"
                    }
                }
            });
            // XP Black Bar 1;
            var barH = 8;
            var barTxtSize = 9;
            var xpBlackBar1Id = $"{id}.blackbar.1";
            CreateProgressBar(basePlayer, tracked, xpBlackBar1Id, topId, "0 1", "1 1", $"{xpImgH + padding} {-topH}", $"{0} {-topH + barH}", fadeOut, fadeIn, progressDelay, imgBgColor, barColor, notification.OldItemLevel, notification.NewItemLevel, notification.ItemXPGained, notification.OldItemXP, notification.NewItemXP, textColor, barTxtSize, notification.IsItemLevelup, topId, 0.25f);


            // Bottom
            var bottomId = $"{leftId}.bottom";
            var bottomH = topH;
            tracked.Add(new CuiElement
            {
                Name = bottomId,
                Parent = leftId,
                FadeOut = fadeOut,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0",
                        FadeIn = fadeIn
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 0",
                        OffsetMin = $"{0} {0}",
                        OffsetMax = $"{0} {bottomH}"
                    }
                }
            });

            var xpBlackBar2Id = $"{id}.blackbar.2";
            CreateProgressBar(basePlayer, tracked, xpBlackBar2Id, bottomId, "0 0", "1 0", $"{xpImgH + padding} {0}", $"{0} {barH}", fadeOut, fadeIn, progressDelay, imgBgColor, barColor, notification.OldCategoryLevel, notification.NewCategoryLevel, notification.CategoryXPGained, notification.OldCategoryXP, notification.NewCategoryXP, textColor, barTxtSize, notification.IsCategoryLevelup, bottomId, 0.45f);


            // XP Image BG 2;
            var xpImg2BgId = $"{id}.xpimg2.bg";
            var xpImgP2 = 4;
            tracked.Add(new CuiElement
            {
                Name = xpImg2BgId,
                Parent = bottomId,
                FadeOut = fadeOut,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = imgBgColor,
                        FadeIn = fadeIn,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0 0",
                        OffsetMin = $"{0} {0}",
                        OffsetMax = $"{xpImgH} {xpImgH}"
                    }
                }
            });
            // XP Image 2;
            tracked.Add(new CuiElement
            {
                Name = $"{id}.xpimg2",
                Parent = xpImg2BgId,
                FadeOut = fadeOut,
                Components =
                {
                    new CuiImageComponent
                    {
                        Png = PLUGIN.ImageLibrary?.Call<string>("GetImage", $"qc.category.{notification.Category}"),
                        Color = "1 1 1 1",
                        FadeIn = fadeIn,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = $"{xpImgP2} {xpImgP2}",
                        OffsetMax = $"{-xpImgP2} {-xpImgP2}"
                    }
                }
            });

            // Glow Element
            if (notification.IsRare)
            {
                var glowId = $"{id}.glow";
                tracked.Add(new CuiElement
                {
                    Name = glowId,
                    Parent = id,
                    FadeOut = 0.5f,
                    Components =
                {
                    new CuiImageComponent
                    {
                        Color = "1 1 1 0.7",
                        FadeIn = 0.05f
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }
                }
                });
                // Start Glow Destroy Timer
                PLUGIN.timer.In(0.2f, () =>
                {
                    CuiHelper.DestroyUi(basePlayer, glowId);
                });
            }


            container.AddRange(tracked);
            CuiHelper.DestroyUi(basePlayer, id);
            CuiHelper.AddUi(basePlayer, container);
            PLUGIN.PlaySfx(basePlayer, notification.IsRare ? PLUGIN.config.SFX.ItemCraftedRare : PLUGIN.config.SFX.ItemCraftedNormal);
            if (notification.IsDuplicated)
            {
                PLUGIN.PlaySfx(basePlayer, PLUGIN.config.SFX.ItemDuplicated);
            }
            PLUGIN.timer.In(pause, () =>
            {
                foreach (var item in tracked)
                {
                    CuiHelper.DestroyUi(basePlayer, item.Name);
                }
            });
        }
        #endregion
    }
}

namespace Oxide.Plugins
{
    partial class QualityCrafting : CovalencePlugin
    {
        public static class Styles
        {
            public static readonly float FadeIn = 0.1f;
            public static readonly float FadeOut = 0.6f;
            public static readonly float Pause = 2f;
            public static readonly string Material = "assets/content/materials/highlight.png";
            public static readonly int NotificationPadding = 8;
        }
    }
}

namespace Oxide.Plugins
{
	partial class QualityCrafting : CovalencePlugin
	{
		private readonly string TRACKING_HUD_ID = "qc.hud.tracking";

		public void DestroyTrackingHud(BasePlayer basePlayer)
        {
			CuiHelper.DestroyUi(basePlayer, TRACKING_HUD_ID);
        }

		public void ShowTrackingHud(BasePlayer basePlayer)
		{
			var category = TrackingManager.GetTrackedCategory(basePlayer);
            var skills = CraftingManager.GetSkills(basePlayer);
            var id = TRACKING_HUD_ID;
            if (category == null)
            {
				DestroyTrackingHud(basePlayer);
				return;
            }
            var x = config.HUD.TrackedSkill.X;
            var y = config.HUD.TrackedSkill.Y;
            var h = 40;
            var w = config.HUD.TrackedSkill.Size;
            var padding = 4;
            var icon = $"qc.category.{category.Name}";
            var imgBgColor = "0 0 0 0.8";
            var tracked = new List<CuiElement>();
            // Base
            tracked.Add(new CuiElement
            {
                Parent = "Hud",
                Name = id,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0.5 0.5 0.5 0.5"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = AnchorDefault,
                        AnchorMax = AnchorDefault,
                        OffsetMin = $"{x-GlobalOffset} {y}",
                        OffsetMax = $"{x+w-GlobalOffset} {y+h}"
                    }
                }
            });
            // Image Background
            var imgBgId = $"{id}.img.bg";
            tracked.Add(new CuiElement
            {
                Name = imgBgId,
                Parent = id,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = imgBgColor
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0 0",
                        OffsetMin = $"{padding} {padding}",
                        OffsetMax = $"{h-padding} {h-padding}"
                    }
                }
            });
            // Image
            var imgP = 4;
            var imgId = $"{id}.img";
            tracked.Add(new CuiElement
            {
                Name = imgId,
                Parent = imgBgId,
                Components =
                {
                    new CuiImageComponent
                    {
                        Png = PLUGIN.ImageLibrary?.Call<string>("GetImage", icon)
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = $"{imgP} {imgP}",
                        OffsetMax = $"{-imgP} {-imgP}"
                    }
                }
            });
            // Black bar
            var barH = 8;
            var barColor = config.Colors.XPBar;
            var blackBarId = $"{id}.blackbar";
            var xpProg = skills.GetLevelPercent(category);
            var level = skills.GetLevel(category);
            var isMaxLevel = level >= 100;
            var textSize = 10;
            var textColor = config.Colors.Text;
            var curLevelXp = level == 0 ? 0 : category.GetCategoryLevelXPReq(level);
            var xpAmt = skills.GetXP(category) - curLevelXp;
            var xpReq = category.GetCategoryLevelXPReq(level + 1) - curLevelXp;
            var transparent = "0 0 0 0";
            tracked.Add(new CuiElement
            {
                Name = blackBarId,
                Parent = id,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = isMaxLevel ? transparent : imgBgColor
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 0",
                        OffsetMin = $"{h + padding} {padding}",
                        OffsetMax = $"{-padding} {padding + barH}"
                    }
                }
            });
            // Green Bar
            tracked.Add(new CuiElement
            {
                Parent = blackBarId,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = isMaxLevel ? transparent: barColor
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = $"{xpProg} 1"
                    }
                }
            });
            // Level Text
            tracked.Add(new CuiElement
            {
                Parent = blackBarId,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = $"{category.DisplayName(basePlayer)}\n{level}",
                        FontSize = textSize,
                        Color = textColor,
                        Align = UnityEngine.TextAnchor.LowerLeft
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",
                        AnchorMax = $"1 1",
                        OffsetMin = $"{0} {2}",
                        OffsetMax = $"{0} {35}"
                    }
                }
            });
            // XP Text
            tracked.Add(new CuiElement
            {
                Parent = blackBarId,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = isMaxLevel ? Lang("max level", basePlayer) : $"{xpAmt}/{xpReq} {Lang("xp", basePlayer)}",
                        FontSize = textSize,
                        Color = textColor,
                        Align = UnityEngine.TextAnchor.LowerRight
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "1 1",
                        AnchorMax = $"1 1",
                        OffsetMin = $"{-50} {2}",
                        OffsetMax = $"{0} {35}"
                    }
                }
            });

            var container = new CuiElementContainer();
            container.AddRange(tracked);
            DestroyTrackingHud(basePlayer);
            CuiHelper.AddUi(basePlayer, container);
        }
	}
}

namespace Oxide.Plugins
{
    partial class QualityCrafting
    {

        private T LoadDataFile<T>(string fileName)
        {
            try
            {
                return Interface.Oxide.DataFileSystem.ReadObject<T>($"{Name}/{fileName}");
            }
            catch (Exception)
            {
                return default(T);
            }
        }

        private static string ColorToHex(string color)
        {
            var split = color.Split(' ');
            var r = (int) Math.Round(float.Parse(split[0]) * 255f);
            var g = (int)Math.Round(float.Parse(split[1]) * 255f);
            var b = (int)Math.Round(float.Parse(split[2]) * 255f);
            return "#" + r.ToString("X2") + g.ToString("X2") + b.ToString("X2");
        }

        private static string ColorTextHex(string text, string hexColor)
        {
            return $"<color={hexColor}>{text}</color>";
        }

        private static string ColorTextRGB(string text, string rgb)
        {
            return ColorTextHex(text, ColorToHex(rgb));
        }

        public string Offset(int x, int y)
        {
            return $"{x} {y}";
        }

        public int GlobalOffset = 640;
        public string AnchorDefault => "0.5 0";

        public string Anchor(float x, float y)
        {
            return $"{x} {y}";
        }

        private static string SetOpacity(string colorString, float opacity)
        {
            var split = colorString.Split(' ');
            split[3] = $"{opacity}";
            return string.Join(" ", split);
        }

        private void SaveDataFile<T>(string fileName, T data)
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/{fileName}", data);
        }

        private void PlaySfx(BasePlayer player, string sound) => EffectNetwork.Send(new Effect(sound, player, 0, Vector3.zero, Vector3.forward), player.net.connection);

        public class SecurityCheck
        {
            public bool Success { get; set; }
            public string[] Args { get; set; }

            public SecurityCheck(string[] args)
            {
                Guid guidValue;
                if (args.Length > 0 && Guid.TryParse(args[0], out guidValue) && guidValue == PLUGIN.Secret)
                {
                    Success = true;
                    Args = args.Skip(1).ToArray();
                }
                else
                {
                    Success = false;
                    Args = args;
                }
            }
        }
    }
}

namespace Oxide.Plugins
{
    partial class QualityCrafting : CovalencePlugin
    {
        public class PlayerSkillSheet
        {
            // <SkillCategory.ID, xp>
            public Dictionary<int, uint> _categoryXP = new Dictionary<int, uint>();
            // <Item.id, xp>
            public Dictionary<int, uint> _itemXP = new Dictionary<int, uint>();

            private static int[] _ItemXpToLevelCache = null;

            private static int[] _CategoryXpToLevelCache = null;

            public PlayerSkillSheet()
            {
                foreach(SkillCategory category in SkillCategory.ALL)
                {
                    _categoryXP[category.ID] = 0;
                }
            }

            public PlayerSkillSheet SetLevel(Item item, int level)
            {
                var category = SkillCategory.GetByItem(item);
                var xp = category.GetItemLevelXPReq(level);
                _itemXP[item.info.itemid] = xp;
                return this;
            }

            public PlayerSkillSheet SetLevel(SkillCategory category, int level)
            {
                if (SkillCategory.IsCategoryEnabled(category))
                {
                    var xp = category.GetCategoryLevelXPReq(level);
                    _categoryXP[category.ID] = xp;
                }
                return this;
            }

            public List<int> GetCategoryItemIDs(SkillCategory category)
            {
                var list = new List<int>();
                foreach(var id in _itemXP.Keys)
                {
                    var item = ItemManager.CreateByItemID(id);
                    if (SkillCategory.GetByItem(item) == category)
                    {
                        list.Add(id);
                    }
                }
                return list;
            }

            public int GetLevel(Item item)
            {
                var xp = GetXP(item);
                return GetItemLevelFromXp(item, xp);
            }

            public int GetLevelFromItemId(int itemId)
            {
                var item = ItemManager.CreateByItemID(itemId);
                return GetLevel(item);
            }

            public int GetLevel(SkillCategory category)
            {
                if (!SkillCategory.IsCategoryEnabled(category))
                {
                    return 0;
                }
                var xp = GetXP(category);
                return GetLevelFromXp(category, xp);
            }

            public float GetCraftingSpeedMultiplier(SkillCategory category)
            {
                return !SkillCategory.IsCategoryEnabled(category) ? 1f : 1f + (GetLevel(category) * PLUGIN.config.Categories[category.Name.TitleCase()].PerkIncreasesPerLevel.CraftingSpeed);
            }

            public int GetDuplicateChance(SkillCategory category)
            {
                return !SkillCategory.IsCategoryEnabled(category) ? 0 : (int) Math.Floor(PLUGIN.config.Categories[category.Name.TitleCase()].PerkIncreasesPerLevel.DuplicateChance * GetLevel(category) * 100);
            }

            public PlayerSkillSheet GrantXP(SkillCategory category, uint xp)
            {
                _categoryXP[category.ID] += xp;
                return this;
            }

            public PlayerSkillSheet GrantXP(Item item, uint xp)
            {
                if (!_itemXP.ContainsKey(item.info.itemid))
                {
                    _itemXP[item.info.itemid] = xp;
                }
                else
                {
                    _itemXP[item.info.itemid] += xp;
                }
                return this;
            }

            public uint GetXP(SkillCategory category)
            {
                uint catXP = 0;
                if (SkillCategory.IsCategoryEnabled(category) && _categoryXP.TryGetValue(category.ID, out catXP))
                {
                    return catXP;
                }
                return 0;
            }

            public uint GetXP(Item item)
            {
                uint itemXP = 0;
                if (_itemXP.TryGetValue(item.info.itemid, out itemXP))
                {
                    return itemXP;
                }
                return 0;
            }

            public float GetLevelPercent(Item item)
            {
                var category = SkillCategory.GetByItem(item);
                if (!SkillCategory.IsCategoryEnabled(category))
                {
                    return 0;
                }
                int level = GetLevel(item);
                int nextLevel = level + 1;
                var req1 = level > 0 ? category.GetItemLevelXPReq(level) : 0;
                var req2 = category.GetItemLevelXPReq(nextLevel);
                var xp = GetXP(item);
                var xpProg = xp - req1;
                return Math.Max(0f, Math.Min(1f, (float)xpProg / (float)(req2 - req1)));
            }

            public float GetLevelPercent(SkillCategory category)
            {
                if (!SkillCategory.IsCategoryEnabled(category))
                {
                    return 0;
                }
                int level = GetLevel(category);
                int nextLevel = level + 1;
                var req1 = level > 0 ? category.GetCategoryLevelXPReq(level) : 0;
                var req2 = category.GetCategoryLevelXPReq(nextLevel);
                var xp = GetXP(category);
                var xpProg = xp - req1;
                return Math.Max(0f, Math.Min(1f, (float)xpProg / (float)(req2 - req1)));
            }

            private static int GetItemLevelFromXp(Item item, uint xp)
            {
                var category = SkillCategory.GetByItem(item);
                if (!SkillCategory.IsCategoryEnabled(category))
                {
                    return 0;
                }
                if (_ItemXpToLevelCache == null)
                {
                    _ItemXpToLevelCache = new int[category.GetItemLevelXPReq(100)];
                    for (int i = 0; i < 100; i++)
                    {
                        var minXp = (int)category.GetItemLevelXPReq(i);
                        var maxXp = (int)category.GetItemLevelXPReq(i + 1);
                        for (int j = minXp; j < maxXp; j++)
                        {
                            _ItemXpToLevelCache[j] = i;
                        }
                    }
                }
                if (xp >= _ItemXpToLevelCache.Length)
                {
                    return 100;
                }
                return _ItemXpToLevelCache[(int)xp];
            }

            private static int GetLevelFromXp(SkillCategory category, uint xp)
            {
                if (!SkillCategory.IsCategoryEnabled(category))
                {
                    return 0;
                }
                if (_CategoryXpToLevelCache == null)
                {
                    _CategoryXpToLevelCache = new int[category.GetCategoryLevelXPReq(100)];
                    for (int i = 0; i < 100; i++)
                    {
                        var minXp = category.GetCategoryLevelXPReq(i);
                        var maxXp = category.GetCategoryLevelXPReq(i + 1);
                        for (uint j = minXp; j < maxXp; j++)
                        {
                            _CategoryXpToLevelCache[j] = i;
                        }
                    }
                }
                if (xp >= _CategoryXpToLevelCache.Length)
                {
                    return 100;
                }
                return _CategoryXpToLevelCache[xp];
            }

            public override string ToString()
            {
                var sb = new StringBuilder();
                foreach (SkillCategory category in SkillCategory.ALL)
                {
                    sb.Append($"{category.Name}: {GetLevel(category)}; ");
                }
                return sb.ToString();
            }
        }
    }
}

namespace Oxide.Plugins
{
    partial class QualityCrafting : CovalencePlugin
    {
        public class SkillCategory
        {
            public SkillCategory(int id, string name, string defaultIcon)
            {
                this.ID = id;
                this.DefaultIcon = defaultIcon;
                this.Name = name;
            }

            public float? TemporaryMultiplier { get; set; } = null;

            private static List<uint> _itemLevelXpRequirements = null;
            private static List<uint> _categoryLevelXpRequirements = null;

            public static readonly SkillCategory GUNSMITHING = new SkillCategory(0, "gunsmithing", "https://i.imgur.com/BH0ndEH.png");
            public static readonly SkillCategory TOOLCRAFTING = new SkillCategory(1, "toolcrafting", "https://i.imgur.com/I0i8tBW.png");
            public static readonly SkillCategory TAILORING = new SkillCategory(2, "tailoring", "https://i.imgur.com/WlrTTKR.png");
            public static readonly SkillCategory BOWMAKING = new SkillCategory(3, "bowmaking", "https://i.imgur.com/NsBOZg1.png");
            public static readonly SkillCategory WEAPONSMITHING = new SkillCategory(4, "weaponsmithing", "https://i.imgur.com/1eAdvAq.png");
            public static readonly List<SkillCategory> ALL = new List<SkillCategory>()
            {
                GUNSMITHING,
                TOOLCRAFTING,
                TAILORING,
                BOWMAKING,
                WEAPONSMITHING
            };
            public int ID { get; private set; }
            public string DefaultIcon { get; private set; }
            public string Name { get; private set; }
            public string NameTitleCase
            {
                get
                {
                    return Name.TitleCase();
                }
            }
            public uint MinLevelXP { get; private set; }
            public uint MidLevelXP { get; private set; }
            public uint MaxLevelXP { get; private set; }
            public string DisplayName(BasePlayer basePlayer)
            {
                return PLUGIN.Lang(Name, basePlayer);
            }

            public static SkillCategory GetByName(string name)
            {
                var category = ALL.FirstOrDefault(x => x.Name == name);
                return category == null || !IsCategoryEnabled(category) ? null : category;
            }

            public static SkillCategory GetDefaultCategory()
            {
                foreach(var category in ALL.OrderBy(x => x.ID))
                {
                    if (IsCategoryEnabled(category))
                    {
                        return category;
                    }
                }
                return null;
            }

            public static SkillCategory GetByID(int ID)
            {
                var category = ALL[ID];
                return category == null || !IsCategoryEnabled(category) ? null : category;
            }

            public static SkillCategory GetByItemName(string itemDisplayName)
            {
                var itemDef = ItemManager.itemList.FirstOrDefault(x => x.displayName.translated.ToLower() == itemDisplayName.ToLower());
                return itemDef == null ? null : GetByItemDefinition(itemDef);
            }

            public static SkillCategory GetByItemDefinition(ItemDefinition itemDefinition)
            {
                if (itemDefinition == null)
                {
                    return null;
                }
                try
                {
                    var item = ItemManager.Create(itemDefinition);
                    if (item == null)
                    {
                        return null;
                    }
                    return GetByItem(item);
                }
                catch (Exception e)
                {
                    return null;
                }
            }

            public static SkillCategory GetByItem(Item item)
            {
                var heldEntity = item.GetHeldEntity();
                if (heldEntity == null && item.info.isWearable && item.info.category == ItemCategory.Attire)
                    return IsCategoryEnabled(TAILORING) ? SkillCategory.TAILORING : null;
                if (heldEntity == null)
                    return null;
                if (heldEntity is CompoundBowWeapon || heldEntity is BowWeapon || heldEntity is CrossbowWeapon)
                    return IsCategoryEnabled(BOWMAKING) ? SkillCategory.BOWMAKING : null;
                if (heldEntity is BaseProjectile || heldEntity.GetType().IsSubclassOf(typeof(BaseProjectile)) && item.info.category == ItemCategory.Weapon)
                    return IsCategoryEnabled(GUNSMITHING) ? SkillCategory.GUNSMITHING : null;
                if (heldEntity is BaseMelee || heldEntity.GetType().IsSubclassOf(typeof(BaseMelee)))
                    if (item.info.category == ItemCategory.Weapon)
                    {
                        return IsCategoryEnabled(WEAPONSMITHING) ? SkillCategory.WEAPONSMITHING : null;
                    }
                    else
                    {
                        return IsCategoryEnabled(TOOLCRAFTING) ? SkillCategory.TOOLCRAFTING : null;
                    }
                return null;
            }

            public static bool IsCategoryEnabled(SkillCategory category)
            {
                if (category == null)
                {
                    return false;
                }
                var nameTitleCase = category.Name.TitleCase();
                if (PLUGIN.config.Categories.ContainsKey(nameTitleCase))
                {
                    return PLUGIN.config.Categories[category.Name.TitleCase()].Enabled;
                }
                return false;
            }

            public float GetBaseCraftingSpeed()
            {
                return PLUGIN.config.Categories[NameTitleCase].BaseCraftingSpeed;
            }

            public float GetXpMultiplier()
            {
                return TemporaryMultiplier == null ? PLUGIN.config.Categories[NameTitleCase].XPMultiplier : TemporaryMultiplier.Value;
            }

            public uint GetItemLevelXPReq(int level)
            {
                if (level < 1 || level > 100)
                {
                    return 0;
                }
                if (_itemLevelXpRequirements == null)
                {
                    var baseXp = 15ul;
                    var growth = 0.85f;
                    _itemLevelXpRequirements = new List<uint>() { 0 };
                    for(int i = 1; i < 102; i++)
                    {
                        var value = (uint)(baseXp + Math.Pow(level - 1, growth)) + _itemLevelXpRequirements[i - 1];
                        _itemLevelXpRequirements.Add(value);
                    }
                }
                return _itemLevelXpRequirements[level];
            }

            public uint GetCategoryLevelXPReq(int level)
            {
                if (level < 1 || level > 100)
                {
                    return 0;
                }
                if (_categoryLevelXpRequirements == null)
                {
                    _categoryLevelXpRequirements = new List<uint>() { 0 };
                    for (int i = 1; i < 102; i++)
                    {
                        var value = (uint)Math.Round(0.04 * Math.Pow(i+1, 3) + 0.8 * Math.Pow(i+1, 2) + 2 * i+1);
                        _categoryLevelXpRequirements.Add(value);
                    }
                }
                return _categoryLevelXpRequirements[level];
            }
        }
    }
}

namespace Oxide.Plugins
{
    partial class QualityCrafting : CovalencePlugin
    {
        public static class CraftingManager
        {
            // <userIdString, SkillSheet>
            private static Dictionary<string, PlayerSkillSheet> _playerCraftingSkills = new Dictionary<string, PlayerSkillSheet>();

            public static void Clear()
            {
                _playerCraftingSkills.Clear();
            }

            public static void Clear(BasePlayer basePlayer)
            {
                _playerCraftingSkills.Remove(basePlayer.UserIDString);
            }

            public static PlayerSkillSheet GetSkills(BasePlayer basePlayer)
            {
                if (!_playerCraftingSkills.ContainsKey(basePlayer.UserIDString))
                {
                    _playerCraftingSkills[basePlayer.UserIDString] = new PlayerSkillSheet();
                }
                return _playerCraftingSkills[basePlayer.UserIDString];
            }

            public static List<LeaderboardRank> GetTopPlayerSkills(SkillCategory category, int skip = 0, int take = 25)
            {
                return _playerCraftingSkills
                    .Select(x => new LeaderboardRank { UserIdString = x.Key, Level = x.Value.GetLevel(category) })
                    .Where(x => !string.IsNullOrEmpty(x.UserDisplayName))
                    .OrderByDescending(x => x.Level)
                    .Skip(skip)
                    .Take(take)
                    .ToList();
            }

            public static List<CraftChance> GetQualityChances(int itemLevel)
            {
                return new List<CraftChance>
                {
                    new CraftChance{ Quality = 0, Percent = GetRelativeChanceOfQuality(0, itemLevel) },
                    new CraftChance{ Quality = 1, Percent = GetRelativeChanceOfQuality(1, itemLevel) },
                    new CraftChance{ Quality = 2, Percent = GetRelativeChanceOfQuality(2, itemLevel) },
                    new CraftChance{ Quality = 3, Percent = GetRelativeChanceOfQuality(3, itemLevel) },
                    new CraftChance{ Quality = 4, Percent = GetRelativeChanceOfQuality(4, itemLevel) },
                    new CraftChance{ Quality = 5, Percent = GetRelativeChanceOfQuality(5, itemLevel) }
                };
            }

            private static int GetRelativeChanceOfQuality(int quality, int itemLevel)
            {
                if (quality >= 5)
                {
                    return GetChanceOfQuality(quality, itemLevel);
                }
                return GetChanceOfQuality(quality, itemLevel) - GetChanceOfQuality(quality + 1, itemLevel);
            }

            private static int GetChanceOfQuality(int quality, int itemLevel)
            {
                int major = 4;
                int minor = 1;
                int step = 20;
                switch (quality)
                {
                    case 0:
                        return 100;
                    case 1:
                        return (Math.Min(step, itemLevel) * major) + Math.Min(step, Math.Max(0, (itemLevel - 20)) * minor);
                    case 2:
                        return (Math.Min(step, Math.Max(0, itemLevel - 20)) * major) + Math.Min(step, Math.Max(0, (itemLevel - 40)) * minor);
                    case 3:
                        return (Math.Min(step, Math.Max(0, itemLevel - 40)) * major) + Math.Min(step, Math.Max(0, (itemLevel - 60)) * minor);
                    case 4:
                        return (Math.Min(step, Math.Max(0, itemLevel - 60)) * major) + Math.Min(step, Math.Max(0, (itemLevel - 80)) * minor);
                    case 5:
                        return Math.Min(step, Math.Max(0, itemLevel - 80)) * major;
                    default:
                        return 0;
                }
            }

            public static int GetCraftedItemQuality(Item item, PlayerSkillSheet skills)
            {
                int itemLevel = skills.GetLevel(item);
                if (itemLevel == 0)
                {
                    return 0;
                }
                int roll = UnityEngine.Random.Range(1, 100);
                
                // roll for T5
                if (itemLevel > 80)
                {
                    var r = GetChanceOfQuality(5, itemLevel);
                    if (roll <= r)
                    {
                        return 5;
                    }
                }
                // roll for T4
                if (itemLevel > 60)
                {
                    var r = GetChanceOfQuality(4, itemLevel);
                    if (roll <= r)
                    {
                        return 4;
                    }
                }
                // roll for T3
                if (itemLevel > 40)
                {
                    var r = GetChanceOfQuality(3, itemLevel);
                    if (roll <= r)
                    {
                        return 3;
                    }
                }
                // roll for T2
                if (itemLevel > 20)
                {
                    var r = GetChanceOfQuality(2, itemLevel);
                    if (roll <= r)
                    {
                        return 2;
                    }
                }
                // roll for T1
                if (itemLevel > 0)
                {
                    var r = GetChanceOfQuality(1, itemLevel);
                    if (roll <= r)
                    {
                        return 1;
                    }
                }
                // roll for T0
                return 0;
            }

            public static void Load()
            {
                var all = PLUGIN.LoadDataFile<Dictionary<string, PlayerSkillSheet>>("Skills");
                if (all == null)
                {
                    all = new Dictionary<string, PlayerSkillSheet>();
                }
                _playerCraftingSkills = all;
            }

            public static void Save()
            {
                PLUGIN.SaveDataFile("Skills", _playerCraftingSkills);
            }
        }
    }
}

namespace Oxide.Plugins
{
    partial class QualityCrafting : CovalencePlugin
    {
        #region Notification Classes
        public enum NotificationType
        {
            ItemCrafted,
            GainedXP
        }
        public abstract class Notification
        {
            public abstract NotificationType Type { get; }
        }
        public class NotificationBundle
        {
            public List<Notification> Items { get; set; }
            public bool ContainsLevelup { get; set; }
        }
        public class GainedXPNotification : Notification
        {
            public override NotificationType Type { get; } = NotificationType.GainedXP;
            public string Icon { get; set; }
            public string SkillDisplayName { get; set; }
            public float OldXP { get; set; }
            public float NewXP { get; set; }
            public int OldLevel { get; set; }
            public int NewLevel { get; set; }
            public uint XPGained { get; set; }
            public bool IsLevelUp { get; set; }
        }
        public class ItemCraftedNotification : Notification
        {
            public override NotificationType Type { get; } = NotificationType.ItemCrafted;
            public string SkillDisplayName { get; set; }
            public string Icon { get; set; }
            public bool IsItem { get; set; }
            public int OldCategoryLevel { get; set; }
            public int NewCategoryLevel { get; set; }
            public int OldItemLevel { get; set; }
            public int NewItemLevel { get; set; }
            public float OldCategoryXP { get; set; }
            public float NewCategoryXP { get; set; }
            public float OldItemXP { get; set; }
            public float NewItemXP { get; set; }
            public bool IsCategoryLevelup { get; set; }
            public bool IsItemLevelup { get; set; }
            public uint CategoryXPGained { get; set; }
            public uint ItemXPGained { get; set; }
            public int Quality { get; set; }
            public string Category { get; set; }
            public bool IsRare { get; set; }
            public bool IsDuplicated { get; set; }

            public override string ToString()
            {
                return $"{SkillDisplayName}; {Icon}; {IsItem}; {OldCategoryLevel}; {NewCategoryLevel}; {OldCategoryXP}; {NewCategoryXP}; {IsCategoryLevelup}; {CategoryXPGained};";
            }
        }
        #endregion

        public static class NotificationManager
        {
            private static Dictionary<string, List<NotificationBundle>> PlayerNotifications = new Dictionary<string, List<NotificationBundle>>();

            public static void DestroyAllNotifications(BasePlayer basePlayer)
            {
                CuiHelper.DestroyUi(basePlayer, NOTIFICATION_ID);
                PLUGIN.timer.In(5, () =>
                {
                    if (basePlayer != null)
                    {
                        CuiHelper.DestroyUi(basePlayer, NOTIFICATION_ID);
                    }
                });
            }

            public static void AddNotifications(BasePlayer basePlayer, params Notification[] notifications)
            {
                var userIdString = basePlayer.UserIDString;
                if (!PlayerNotifications.ContainsKey(userIdString))
                {
                    PlayerNotifications.Add(userIdString, new List<NotificationBundle>());
                }
                var items = new List<Notification>();
                foreach(var notification in notifications)
                {
                    items.Add(notification);
                }
                PlayerNotifications[userIdString].Add(new NotificationBundle { Items = items });
                if (PlayerNotifications[userIdString].Count <= 1)
                {
                    ShowNotifications(basePlayer);
                }
            }

            private static void ShowNotifications(BasePlayer basePlayer)
            {
                string userIdString = basePlayer.UserIDString;
                if (PlayerNotifications.ContainsKey(userIdString) && PlayerNotifications[userIdString].Count > 0)
                {
                    var bundle = PlayerNotifications[userIdString][0];
                    float totalTime = 3.5f;
                    foreach (var item in bundle.Items)
                    {
                        if (item.Type == NotificationType.ItemCrafted && PLUGIN.config.Notifications.ItemCraftedNotification.Show)
                        {
                            var casted = (ItemCraftedNotification)item;
                            PLUGIN.ShowItemCraftedNotification(basePlayer, casted);
                        }
                        else if (item.Type == NotificationType.GainedXP && PLUGIN.config.Notifications.LevelUpItemNotification.Show)
                        {
                            var casted = (GainedXPNotification)item;
                            PLUGIN.ShowXpGainedNotification(basePlayer, casted);
                        }
                    }
                    PLUGIN.timer.In(totalTime, () =>
                    {
                        if (PlayerNotifications.ContainsKey(userIdString) && PlayerNotifications[userIdString].Count > 0)
                        {
                            PlayerNotifications[userIdString].RemoveAt(0);
                            ShowNotifications(basePlayer);
                        }
                    });
                }
            }
        }
    }
}

namespace Oxide.Plugins
{
    partial class QualityCrafting : CovalencePlugin
    {
        public static class QualityItemManager
        {
            public static QualityItem SetItemQuality(Item item, int quality, BasePlayer creator = null)
            {
                item.text = $"{quality}{(creator == null ? string.Empty : creator.UserIDString)}";
                return new QualityItem(item);
            }

            public static bool IsQualityItem(Item item)
            {
                return !string.IsNullOrEmpty(item.text);
            }

            public static QualityItem GetByItem(Item item)
            {
                return new QualityItem(item);
            }

            private static float GetModifier(int quality, float modifier)
            {
                return 1f + (modifier * quality);
            }

            public static float GetClothingStatResistance(BasePlayer basePlayer, Rust.DamageType dt, bool baseValues = false)
            {
                var total = 0f;
                foreach (Item item in basePlayer.inventory.containerWear.itemList)
                {
                    /* Protection increase by 1.5 */
                    /* Scale by 1.5 - 1 = 0.5 */
                    try
                    {
                        var value = item.info.ItemModWearable.protectionProperties.Get(dt);
                        if (value <= 0)
                        {
                            continue;
                        }
                        if (!baseValues)
                        {
                            int quality = QualityItemManager.GetItemQuality(item);
                            value += (quality * PLUGIN.config.Qualities.Modifiers.Protection);
                        }
                        total += value;
                    } catch(Exception) { continue; }
                }
                return total;
            }

            public static List<QualityItemStat> GetItemStats(Item item)
            {
                var list = new List<QualityItemStat>();
                var quality = GetItemQuality(item);
                var category = SkillCategory.GetByItem(item);
                var heldEntity = item.GetHeldEntity();
                if (heldEntity is BaseProjectile)
                {
                    list.Add(new QualityItemStat
                    {
                        StatName = "Damage",
                        PercentModified = PLUGIN.config.Qualities.Modifiers.ProjectileDamage * quality
                    });
                }
                if (heldEntity is BaseMelee)
                {
                    list.Add(new QualityItemStat
                    {
                        StatName = "Damage",
                        PercentModified = PLUGIN.config.Qualities.Modifiers.MeleeDamage * quality
                    });
                    list.Add(new QualityItemStat
                    {
                        StatName = "Gathering",
                        PercentModified = PLUGIN.config.Qualities.Modifiers.GatherRate * quality
                    });
                }
                if (heldEntity == null && item.info.isWearable)
                {
                    list.Add(new QualityItemStat
                    {
                        StatName = "Protection",
                        PercentModified = PLUGIN.config.Qualities.Modifiers.Protection * quality
                    });
                }
                if (item.hasCondition)
                {
                    list.Add(new QualityItemStat
                    {
                        StatName = "Durability",
                        PercentModified = PLUGIN.config.Qualities.Modifiers.Durability * quality
                    });
                }
                return list;
            }

            public static bool IsRareQuality(int quality, int level)
            {
                switch(quality)
                {
                    case 1:
                        return level <= 20;
                    case 2:
                        return level <= 40;
                    case 3:
                        return level <= 60;
                    case 4:
                        return level <= 80;
                    case 5:
                        return level <= 100;
                    default:
                        return false;
                }
            }

            private static HashSet<ulong> AuditedEntities = new HashSet<ulong>();

            public static void AuditQualityItem(ref Item item, BaseEntity heldEntity)
            {
                if (AuditedEntities.Contains(item.uid.Value))
                {
                    return;
                }
                var quality = GetItemQuality(item);
                var compareItem = ItemManager.CreateByItemID(item.info.itemid);
                if (heldEntity is BaseProjectile)
                {
                    var casted = (BaseProjectile)heldEntity;
                    var compareHeldEntity = (BaseProjectile)compareItem.GetHeldEntity();
                    var expected = compareHeldEntity.damageScale * GetModifier(quality, PLUGIN.config.Qualities.Modifiers.ProjectileDamage);
                    if (casted.damageScale != expected)
                    {
                        casted.damageScale = expected;
                    }
                }
                else if (heldEntity is BaseMelee)
                {
                    var casted = (BaseMelee)heldEntity;
                    var compareHeldEntity = (BaseMelee)compareItem.GetHeldEntity();
                    var gather = PLUGIN.config.Qualities.Modifiers.GatherRate;
                    // flesh
                    var expected = compareHeldEntity.gathering.Flesh.gatherDamage * GetModifier(quality, gather);
                    if (casted.gathering.Flesh.gatherDamage != expected)
                    {
                        casted.gathering.Flesh.gatherDamage = expected;
                    }
                    // ore
                    expected = compareHeldEntity.gathering.Ore.gatherDamage * GetModifier(quality, gather);
                    if (casted.gathering.Ore.gatherDamage != expected)
                    {
                        casted.gathering.Ore.gatherDamage = expected;
                    }
                    // tree
                    expected = compareHeldEntity.gathering.Tree.gatherDamage * GetModifier(quality, gather);
                    if (casted.gathering.Tree.gatherDamage != expected)
                    {
                        casted.gathering.Tree.gatherDamage = expected;
                    }
                    // melee
                    var melee = PLUGIN.config.Qualities.Modifiers.MeleeDamage;
                    foreach (DamageTypeEntry entry in casted.damageTypes)
                    {
                        var compareEntry = compareHeldEntity.damageTypes.First(x => x.type == entry.type);
                        expected = compareEntry.amount * GetModifier(quality, melee);
                        if (expected != entry.amount)
                        {
                            entry.amount = expected;
                        }
                    }
                }
                AuditedEntities.Add(item.uid.Value);
            }

            public static Item ApplyQualityModifiers(Item item, int quality, bool initial = true)
            {
                if (initial)
                {
                    item.maxCondition *= GetModifier(quality, PLUGIN.config.Qualities.Modifiers.Durability);
                    item.condition = item.maxCondition;
                }
                var heldEntity = item.GetHeldEntity();
                if (heldEntity is BaseProjectile)
                {
                    var casted = (BaseProjectile)heldEntity;
                    casted.damageScale *= GetModifier(quality, PLUGIN.config.Qualities.Modifiers.ProjectileDamage);
                }
                else if (heldEntity is BaseMelee)
                {
                    var casted = (BaseMelee)heldEntity;
                    var melee = PLUGIN.config.Qualities.Modifiers.MeleeDamage;
                    var gather = PLUGIN.config.Qualities.Modifiers.GatherRate;
                    casted.gathering.Flesh.gatherDamage *= GetModifier(quality, gather);
                    casted.gathering.Ore.gatherDamage *= GetModifier(quality, gather);
                    casted.gathering.Tree.gatherDamage *= GetModifier(quality, gather);
                    foreach (DamageTypeEntry entry in casted.damageTypes)
                    {
                        entry.amount *= GetModifier(quality, melee);
                    }
                }
                return item;
            }

            public static int GetItemQuality(Item item)
            {
                return GetByItem(item).Quality;
            }

            public static int GetWorkbenchLevel(Item item)
            {
                return ItemManager.bpList.Where(x => x.targetItem.shortname == item.info.shortname).FirstOrDefault().workbenchLevelRequired;
            }

            public static uint GetItemXpRate(Item item)
            {
                int wbLevel = GetWorkbenchLevel(item);
                switch(wbLevel)
                {
                    case 0: return 2;
                    case 1: return 8;
                    case 2: return 13;
                    case 3: return 50;
                }
                return 0;
            }

            public static string GetColorByQuality(int quality)
            {
                switch(quality)
                {
                    case 1:
                        return PLUGIN.config.Qualities.Tier1.Color;
                    case 2:
                        return PLUGIN.config.Qualities.Tier2.Color;
                    case 3:
                        return PLUGIN.config.Qualities.Tier3.Color;
                    case 4:
                        return PLUGIN.config.Qualities.Tier4.Color;
                    case 5:
                        return PLUGIN.config.Qualities.Tier5.Color;
                    default:
                        return PLUGIN.config.Qualities.Tier0.Color;
                }
            }
        }
    }
}

namespace Oxide.Plugins
{
    partial class QualityCrafting : CovalencePlugin
    {
        public static class TrackingManager
        {
            private static Dictionary<string, int> _playerTracking = new Dictionary<string, int>(); //userIdString: category

            public static void Track(BasePlayer basePlayer, SkillCategory category)
            {
                if (!_playerTracking.ContainsKey(basePlayer.UserIDString))
                {
                    _playerTracking[basePlayer.UserIDString] = category.ID;
                }
                else
                {
                    _playerTracking[basePlayer.UserIDString] = category.ID;
                }
            }

            public static SkillCategory GetTrackedCategory(BasePlayer basePlayer)
            {
                if (_playerTracking.ContainsKey(basePlayer.UserIDString))
                {
                    return SkillCategory.GetByID(_playerTracking[basePlayer.UserIDString]);
                }
                return null;
            }

            public static void Untrack(BasePlayer basePlayer)
            {
                _playerTracking.Remove(basePlayer.UserIDString);
            }

            public static bool IsTracking(BasePlayer basePlayer, SkillCategory category)
            {
                if (!_playerTracking.ContainsKey(basePlayer.UserIDString))
                {
                    return false;
                }
                return _playerTracking[basePlayer.UserIDString].Equals(category.ID);
            }

            public static void Load()
            {
                var all = PLUGIN.LoadDataFile<Dictionary<string, int>>("Tracking");
                if (all == null)
                {
                    all = new Dictionary<string, int>();
                }
                _playerTracking = all;
            }

            public static void Save()
            {
                PLUGIN.SaveDataFile("Tracking", _playerTracking);
            }
        }
    }
}

namespace Oxide.Plugins
{
    partial class QualityCrafting : CovalencePlugin
    {
        public class CommandInfo
        {
            public string Command { get; set; }
            public CommandArgument[] Arguments { get; set; } = new CommandArgument[0];
            public string Description { get; set; }
            public string Method { get; set; }
            public int Rank = 999;
            public string Permission
            {
                get
                {
                    return Permissions == null ? null : Permissions.FirstOrDefault();
                }
                set
                {
                    Permissions = new string[1] { value };
                }
            }
            public string[] Permissions { get; set; } = new string[0];
            public bool AdminOnly
            {
                get
                {
                    return Permissions.Any(x => x.Contains("admin"));
                }
            }

            public int RequiredArgCount { 
                get
                {
                    return Arguments.Where(x => !x.Optional).Count();
                } 
            }

            public int TotalArgCount
            {
                get
                {
                    return Arguments.Length;
                }
            }

            public string ArgString
            {
                get
                {
                    return $"{string.Join(" ", Arguments.Select(x => x.ToString()))}";
                }
            }

            public string Usage(IPlayer player, string prefix)
            {
                return PLUGIN.Lang("usage", player.Id, $"/{prefix} {Command} {ArgString}");
            }

            public void Execute(IPlayer player, string command, string[] args)
            {
                PLUGIN.Call(Method, player, command, args);
            }

            public ValidationResponse Validate(params string[] args)
            {
                if (args.Length < RequiredArgCount || args.Length > TotalArgCount)
                {
                    return new ValidationResponse(ValidationStatusCode.INVALID_LENGTH, RequiredArgCount, TotalArgCount);
                }
                int i = 0;
                foreach(var arg in args)
                {
                    var Argument = Arguments[i];
                    var resp = Argument.Validate(arg);
                    if (!resp.IsValid)
                    {
                        switch(resp.StatusCode)
                        {
                            case ValidationStatusCode.INVALID_VALUE:
                            case ValidationStatusCode.PLAYER_NOT_FOUND:
                                resp.SetData(arg);
                                break;
                        }
                        return resp;
                    }
                    i++;
                }
                return new ValidationResponse();
            }
        }

        public class CommandArgument
        {
            public static readonly CommandArgument PLAYER_NAME = new CommandArgument
            {
                Parameter = "player",
                Validate = (value) =>
                {
                    return BasePlayer.FindAwakeOrSleeping(value) == null ? new ValidationResponse(ValidationStatusCode.PLAYER_NOT_FOUND) : new ValidationResponse(ValidationStatusCode.SUCCESS);
                }
            };

            public string Parameter { get; set; }
            public bool Optional { get; set; } = false;
            public string[] AllowedValues
            {
                set
                {
                    Validate = (given) =>
                    {
                        var expected = value;
                        return expected.Any(x => x.ToLower() == given.ToLower()) ? new ValidationResponse() : new ValidationResponse(ValidationStatusCode.VALUE_NOT_ALLOWED, given, expected);
                    };
                }
            }
            public Func<string, ValidationResponse> Validate { get; set; } = ((value) => { return new ValidationResponse(); });

            public override string ToString()
            {
                return $"<{Parameter}{(Optional ? "?" : string.Empty)}>";
            }
        }

        public class ValidationResponse
        {
            public bool IsValid
            {
                get
                {
                    return StatusCode == ValidationStatusCode.SUCCESS;
                }
            }
            public ValidationStatusCode StatusCode { get; }
            public object[] Data { get; private set; } = new object[0];

            public ValidationResponse()
            {
                StatusCode = ValidationStatusCode.SUCCESS;
            }

            public ValidationResponse(ValidationStatusCode statusCode)
            {
                StatusCode = statusCode;
            }

            public ValidationResponse(ValidationStatusCode statusCode, params object[] data)
            {
                StatusCode = statusCode;
                Data = data;
            }

            public void SetData(params object[] data)
            {
                Data = data;
            }
        }

        public enum ValidationStatusCode
        {
            SUCCESS,
            INVALID_LENGTH,
            INVALID_VALUE,
            PLAYER_NOT_FOUND,
            VALUE_NOT_ALLOWED
        }
    }
}

namespace Oxide.Plugins
{
    partial class QualityCrafting : CovalencePlugin
    {
        public class LeaderboardRank
        {
            public string UserIdString { get; set; }

            public int Level { get; set; }

            [JsonIgnore]
            public string UserDisplayName
            {
                get
                {
                    return UserIdString == null ? string.Empty : BasePlayer.FindByID(ulong.Parse(UserIdString))?.displayName;
                }
            }
        }
    }
}

namespace Oxide.Plugins
{
    partial class QualityCrafting : CovalencePlugin
    {
        public class QualityItem
        {
            public QualityItem(Item item)
            {
                try
                {
                    string text = item.text;
                    if (string.IsNullOrEmpty(text))
                    {
                        Quality = 0;
                        UID = item.uid.Value;
                    }
                    else
                    {
                        Quality = int.Parse(text.Substring(0, 1));
                        if (text.Length > 1)
                        {
                            ulong uid = 0;
                            if (ulong.TryParse(text.Substring(1, text.Length - 1), out uid))
                            {
                                CreatorId = uid;
                            }
                        }
                    }
                } catch(Exception)
                {
                    Quality = 0;
                    UID = item.uid.Value;
                }
            }

            public ulong UID { get; private set; }
            public int Quality { get; private set; }
            public ulong? CreatorId { get; private set; } = null;

            [JsonIgnore]
            public bool HasCreator
            {
                get
                {
                    return CreatorId != null;
                }
            }

            [JsonIgnore]
            public string CreatorDisplayName
            {
                get
                {
                    return CreatorId == null ? string.Empty : BasePlayer.FindByID((ulong)CreatorId)?.displayName;
                }
            }
        }
    }
}

namespace Oxide.Plugins
{
    partial class QualityCrafting : CovalencePlugin
    {
        public class QualityItemStat
        {
            public string StatName { get; set; }
            public float PercentModified { get; set; }
        }
    }
}

namespace Oxide.Plugins
{
    partial class QualityCrafting : CovalencePlugin
    {
        public class CraftChance
        {
            public int Percent { get; set; }
            public int Quality { get; set; }
        }
    }
}
