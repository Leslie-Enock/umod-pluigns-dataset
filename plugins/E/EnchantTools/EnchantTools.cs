using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Enchant Tools", "Default", "1.1.2")]
    [Description("Adds enchanted tools for mining melted resources")]
    public class EnchantTools : RustPlugin
    {
        private List<int> EnchantedTools;

        #region Oxide hooks
        private void Init()
        {
            LoadVariables();
            EnchantedTools = Interface.Oxide?.DataFileSystem?.ReadObject<List<int>>("EnchantTools") ?? new List<int>();

            cmd.AddChatCommand(configData.Command, this, "CmdEnchant");
            cmd.AddConsoleCommand(configData.Command, this, "CcmdEnchant");

            permission.RegisterPermission("enchanttools.admin", this);
            permission.RegisterPermission("enchanttools.use", this);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["PermissionAdmin"] = "You don't have permission to use this command.",
                ["MultiplePlayer"] = "Multiple players found: {0}",
                ["PlayerIsNotFound"] = "The player with the name {0} was not found.",
                ["UsageSyntax"] = "Usage command syntax: \n<color=#FF99CC>{0} <tool_name> <playerName or Id></color>\nAvailable tools names:\n{1}",
                ["ToolGiven"] = "{0} received enchanted tool: {1}.",
                ["CantRepair"] = "You can't repair an enchanted tools.",
                ["ConsoleNotAvailable"] = "This command available only from server console or rcon.",
                ["ConsoleNoPlayerFound"] = "No player with the specified SteamID was found.",
                ["ConsoleNoPlayerAlive"] = "The player with the specified ID was not found among active or sleeping players.",
                ["ConsoleToolGiven"] = "{0} received enchanted tool {1}.",
                ["ConsoleUsageSyntax"] = "Usage command syntax: \n<color=#FF99CC>{0} <tool_name> <steamId></color>\nAvailable tools names:\n{1}"
            }, this);
        }

        private void OnNewSave(string filename)
        {
            EnchantedTools = new List<int>();
            SaveEnchantedTools();
        }

        void OnDispenserBonus(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            BasePlayer player = entity.ToPlayer();
            if (player == null)
            {
                return;
            }

            if (!configData.CanUseByAllPlayers && !permission.UserHasPermission(player.UserIDString, "enchanttool.use"))
            {
                return;
            }

            if (EnchantedTools.Contains(player.GetActiveItem().GetHashCode()))
            {
                switch (item.info.shortname)
                {
                    case "sulfur.ore":
                        //PrintWarning(item.info.shortname);
                        ReplaceContents(-1581843485, ref item);
                        break;
                    case "metal.ore":
                        //PrintWarning(item.info.shortname);
                        ReplaceContents(69511070, ref item);
                        break;
                    case "hq.metal.ore":
                        //PrintWarning(item.info.shortname);
                        ReplaceContents(-1982036270, ref item);
                        break;
                    case "wood":
                        ReplaceContents(-1938052175, ref item);
                        break;
                }
            }
        }

        private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            BasePlayer player = entity.ToPlayer();
            if (player == null)
            {
                return;
            }

            if (!configData.CanUseByAllPlayers && !permission.UserHasPermission(player.UserIDString, "enchanttool.use"))
            {
                return;
            }

            if (EnchantedTools.Contains(player.GetActiveItem().GetHashCode()))
            {
                switch (item.info.shortname)
                {
                    case "sulfur.ore":
                        ReplaceContents(-1581843485, ref item);
                        break;
                    case "metal.ore":
                        ReplaceContents(69511070, ref item);
                        break;
                    case "wolfmeat.raw":
                        ReplaceContents(-813023040, ref item);
                        break;
                    case "meat.boar":
                        ReplaceContents(-242084766, ref item);
                        break;
                    case "hq.metal.ore":
                        //PrintWarning(item.info.shortname);
                        ReplaceContents(-1982036270, ref item);
                        break;
                    case "chicken.raw":
                        ReplaceContents(-1848736516, ref item);
                        break;
                    case "bearmeat":
                        ReplaceContents(-1873897110, ref item);
                        break;
                    case "deermeat.raw":
                        ReplaceContents(-1509851560, ref item);
                        break;
                    case "wood":
                        ReplaceContents(-1938052175, ref item);
                        break;
                }
            }
        }

        private object OnItemRepair(BasePlayer player, Item item)
        {
            if (EnchantedTools.Contains(item.GetHashCode()))
            {
                SendReply(player, lang.GetMessage("CantRepair", this, player.UserIDString));
                return false;
            }

            return null;
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (EnchantedTools.Contains(entity.GetHashCode()))
            {
                EnchantedTools.Remove(entity.GetHashCode());
            }
        }
        #endregion

        #region Chat command
        private void CmdEnchant(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, "enchanttools.admin"))
            {
                SendReply(player, lang.GetMessage("PermissionAdmin", this, player.UserIDString));
                return;
            }

            List<string> shortnames = new List<string>();
            foreach (Tool tool in configData.Tools)
            {
                shortnames.Add(tool.shortname);
            }

            if (args.Length == 2)
            {
                if (shortnames.Contains(args.ElementAtOrDefault(0)))
                {
                    List<BasePlayer> foundPlayers = new List<BasePlayer>();

                    string searchNameOrId = args.ElementAtOrDefault(1);

                    foreach (BasePlayer p in BasePlayer.activePlayerList)
                    {
                        if (p.displayName.Contains(searchNameOrId))
                        {
                            foundPlayers.Add(p);
                        }
                        else if (p.UserIDString == searchNameOrId)
                        {
                            foundPlayers.Add(p);
                        }
                    }
                    if (foundPlayers.Count > 1)
                    {
                        List<string> multiple_names = new List<string>();
                        foreach (BasePlayer p in foundPlayers)
                        {
                            multiple_names.Add(p.displayName);
                        }

                        SendReply(player, lang.GetMessage("MultiplePlayer", this, player.UserIDString), string.Join(", ", multiple_names.ToArray()));
                        return;
                    }
                    else if (foundPlayers.Count == 0)
                    {
                        SendReply(player, lang.GetMessage("PlayerIsNotFound", this, player.UserIDString), args.ElementAtOrDefault(1));
                        return;
                    }
                    else
                    {
                        Tool tool = configData.Tools.Find(t => t.shortname == args.ElementAtOrDefault(0));
                        BasePlayer receiver = foundPlayers.First();
                        if (receiver.IsValid())
                        {
                            GiveEnchantTool(receiver, tool);
                            SendReply(player, lang.GetMessage("ToolGiven", this, player.UserIDString), receiver.displayName, args.ElementAtOrDefault(0));
                            return;
                        }
                    }
                }
            }
            SendReply(player, lang.GetMessage("UsageSyntax", this, player.UserIDString), configData.Command, string.Join(", ", shortnames.ToArray()));
        }
        #endregion

        #region Console command
        private void CcmdEnchant(ConsoleSystem.Arg arg)
        {
            if (!arg.IsServerside && !arg.IsRcon)
            {
                BasePlayer player = arg.Player();
                if (player != null)
                {
                    player.ConsoleMessage(lang.GetMessage("ConsoleNotAvailable", this, player.UserIDString));
                }
            }

            List<string> shortnames = new List<string>();
            foreach (Tool tool in configData.Tools)
            {
                shortnames.Add(tool.shortname);
            }

            string[] args = arg.Args;
            if (args.Length == 2)
            {
                if (shortnames.Contains(args.ElementAtOrDefault(0)))
                {
                    List<BasePlayer> foundPlayers = new List<BasePlayer>();
                    string steamId = args.ElementAtOrDefault(1);

                    IPlayer iplayer = covalence.Players.FindPlayerById(steamId);
                    if (iplayer == null)
                    {
                        SendReply(arg, lang.GetMessage("ConsoleNoPlayerFound", this));
                    }

                    BasePlayer receiver = receiver = BasePlayer.FindAwakeOrSleeping(steamId);
                    if (receiver == null)
                    {
                        SendReply(arg, lang.GetMessage("ConsoleNoPlayerAlive", this));
                    }
                    else
                    {
                        Tool tool = configData.Tools.Find(t => t.shortname == args.ElementAtOrDefault(0));
                        GiveEnchantTool(receiver, tool);
                        SendReply(arg, lang.GetMessage("ConsoleToolGiven", this), receiver.displayName, args.ElementAtOrDefault(0));
                    }
                    return;
                }
            }
            SendReply(arg, lang.GetMessage("ConsoleUsageSyntax", this), configData.Command, string.Join(", ", shortnames.ToArray()));
        }
        #endregion

        #region Helpers
        private void ReplaceContents(int ItemId, ref Item item)
        {
            Item _item = ItemManager.CreateByItemID(ItemId, item.amount);
            if (item != null)
            {
                item.info = _item.info;
                item.contents = _item.contents;
            }
        }

        private void GiveEnchantTool(BasePlayer player, Tool tool)
        {
            Item item = ItemManager.CreateByName(tool.shortname, 1, tool.skinId);
            if (item != null)
            {
                player.GiveItem(item);
                EnchantedTools.Add(item.GetHashCode());
                SaveEnchantedTools();
            }
        }

        private void SaveEnchantedTools()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, EnchantedTools);
        }
        #endregion

        #region Config        
        private ConfigData configData;

        private class ConfigData
        {
            public string Command;
            public bool CanUseByAllPlayers;
            public List<Tool> Tools;
        }

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            ConfigData config = new ConfigData
            {
                Command = "et",
                CanUseByAllPlayers = true,

                Tools = new List<Tool>()
                {
                    new Tool()
                    {
                        shortname = "hatchet",
                        skinId = 0,
                        canRepair = true
                    },
                    new Tool()
                    {
                        shortname = "axe.salvaged",
                        skinId = 0,
                        canRepair = true
                    },
                    new Tool()
                    {
                        shortname = "pickaxe",
                        skinId = 0,
                        canRepair = true
                    },
                    new Tool()
                    {
                        shortname = "icepick.salvaged",
                        skinId = 0,
                        canRepair = true
                    },
                }
            };
            SaveConfig(config);
        }

        private void LoadConfigVariables()
        {
            configData = Config.ReadObject<ConfigData>();
        }

        private void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }
        #endregion

        #region Nested Classes
        private class Tool
        {
            public string shortname;
            public uint skinId;
            public bool canRepair;
        }
        #endregion
    }
}