using System;
using System.Collections.Generic;
using Oxide.Core;
using UnityEngine;
using System.Linq;
using System.IO;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Fancy Grenades", "PaiN", "0.1.3")]
    [Description("Adds different types of grenades to the game.")]
    class FancyGrenades : RustPlugin
    {
        [PluginReference]
        private Plugin Economics, ServerRewards;

        const int GrenadeId = 143803535;
        private Configuration config;
        private ItemDefinition item;

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["PURCHASE_SUCCESS"] = "You have successfully purchased a {0} for {1} {2}",
                ["GIVE_FREE"] = "You have been given a {0}",
                ["NO_PERMISSION"] = "You do not have permission to use this command!",
                ["PURCHASE_FAILED"] = "You do not have enough {0}! You are missing {1} {2}",
                ["INVENTORY_FULL"] = "Your inventory is full!"
            }, this);
        }

        private class Configuration
        {
            [JsonProperty("General Settings")]
            public GeneralSettings settings = new GeneralSettings();

            [JsonProperty("Flashbang Settings")]
            public FlashSettings flash = new FlashSettings();

            [JsonProperty("Molotov Settings")]
            public MollySettings molly = new MollySettings();

            [JsonProperty("Pay Settings")]
            public PaySettings pay = new PaySettings();

        }

        public class GeneralSettings
        {
            [JsonProperty("Purchase Flashbang Command")]
            public string flashCmd = "flash";

            [JsonProperty("Purchase Molotov Command")]
            public string mollyCmd = "molly";

            [JsonProperty("Message SteamID icon")]
            public ulong chatId = 0;
        }

        public class PaySettings
        {
            [JsonProperty("Cost Item (Scrap, ServerRewards, Economics)")]
            public string costItem = "Scrap";

            [JsonProperty("Cost Amount (Disable Cost = 0)")]
            public int costAmount = 100;
        }

        public class MollySettings
        {
            [JsonProperty("Molotov Grenade Name")]
            public string Name = "Incendiary Grenade";

            [JsonProperty("Molotov SkinId")]
            public ulong SkinId = 856483901;
        }

        public class FlashSettings
        {
            [JsonProperty("Flashbang Grenade Name")]
            public string Name = "Stun Grenade";

            [JsonProperty("Flashbang SkinId")]
            public ulong SkinId = 815540662;

            [JsonProperty("Flashbang Range")]
            public int Range = 20;

            [JsonProperty("Flashbang Duration")]
            public float Duration = 2f;

            [JsonProperty("Disable flashing players who are not facing the grenade")]
            public bool NoFlashRule = true;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            string configPath = $"{Interface.Oxide.ConfigDirectory}{Path.DirectorySeparatorChar}{Name}.json";
            PrintWarning($"Could not load a valid configuration file, creating a new configuration file at {configPath}");
            config = new Configuration();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        void Loaded()
        {
            permission.RegisterPermission("fancygrenades.useflash", this);
            permission.RegisterPermission("fancygrenades.ignoreflash", this);
            permission.RegisterPermission("fancygrenades.usemolly", this);
            

            cmd.AddChatCommand(config.settings.flashCmd, this, cmdFlash);
            cmd.AddChatCommand(config.settings.mollyCmd, this, cmdMolly);

        }

        void OnServerInitialized()
        {
            if (config.pay.costItem.Equals("serverrewards", StringComparison.OrdinalIgnoreCase) && !ServerRewards)
            {
                PrintWarning("Selected payment type is set to ServerRewards but ServerRewards plugin is not loaded! Unloading the plugin...");
                Interface.Oxide.UnloadPlugin("FancyGrenades");
                return;
            }

            if (config.pay.costItem.Equals("economics", StringComparison.OrdinalIgnoreCase) && !Economics)
            {
                PrintWarning("Selected payment type is set to Economics but Economics plugin is not loaded! Unloading the plugin...");
                Interface.Oxide.UnloadPlugin("FancyGrenades");
                return;
            }

            if (!config.pay.costItem.Equals("serverrewards", StringComparison.OrdinalIgnoreCase) &&
                !config.pay.costItem.Equals("economics", StringComparison.OrdinalIgnoreCase))
            {
                item = ItemManager.FindItemDefinition(config.pay.costItem);
                if (item == null)
                {
                    PrintWarning($"Item set in config '{config.pay.costItem}' is not correct! Unloading the plugin...");
                    Interface.Oxide.UnloadPlugin("FancyGrenades");
                    return;
                }
            }
        }

        private void OnExplosiveThrown(BasePlayer player, BaseEntity entity, ThrownWeapon wep)
        {
            if (player.IsNpc || entity == null || wep == null) return;

            Item nade = wep?.GetItem();
            if (string.IsNullOrWhiteSpace(nade.name)) return;

            if (permission.UserHasPermission(player.UserIDString, "fancygrenades.useflash"))
            {
                if (nade.name.Equals(config.flash.Name, StringComparison.OrdinalIgnoreCase))
                {
                    timer.Once(2.9f, () =>
                    {
                        entity.Kill();
                        Effect.server.Run("assets/bundled/prefabs/fx/weapons/landmine/landmine_explosion.prefab", entity.transform.position);
                        foreach (var closePlayer in GetPlayersInRadius(entity.transform.position, config.flash.Range))
                        {
                            if (permission.UserHasPermission(closePlayer.UserIDString, "fancygrenades.ignoreflash")) continue;
                            if (config.flash.NoFlashRule && !InNadeAngle(player, entity)) continue;
                            FlashEffect(closePlayer);
                        }
                    });

                    return;
                }
            }
            if (permission.UserHasPermission(player.UserIDString, "fancygrenades.usemolly"))
            {
                if (nade.name.Equals(config.molly.Name, StringComparison.OrdinalIgnoreCase))
                {
                    timer.Once(2.5f, () =>
                    {
                        entity.Kill();
                        FireEffect(entity.transform.position);
                    });
                }
            }
        }
        private void OnExplosiveDropped(BasePlayer player, BaseEntity entity, ThrownWeapon item) => OnExplosiveThrown(player, entity, item);

        [ConsoleCommand("fg.give")]
        void cmdGive(ConsoleSystem.Arg arg)
        {
            if (arg.IsClientside || !arg.IsRcon) return;

            if (arg.Args == null || arg.Args.Length == 0)
            {
                arg.ReplyWith("fg.give <flash/molly> <nameOrIdOrIP>");
                return;
            }

            if (arg.Args[0] != "flash" && arg.Args[0] != "molly")
            {
                arg.ReplyWith("Wrong grenade type specified!");
                return;
            }

            BasePlayer target = Player.Find(arg.Args[1]);
            if (target == null)
            {
                arg.ReplyWith("Target not found!");
                return;
            }

            string nade = arg.Args[0] == "flash" ? config.flash.Name : config.molly.Name;
            DoPayAndGive(target, nade);
            arg.ReplyWith($"Ran command fg.give on {target.displayName}");
        }

        [ConsoleCommand("fg.givefree")]
        void cmdGiveFree(ConsoleSystem.Arg arg)
        {
            if (arg.IsClientside || !arg.IsRcon) return;

            if (arg.Args == null || arg.Args.Length == 0)
            {
                arg.ReplyWith("fg.givefree <flash/molly> <nameOrIdOrIP>");
                return;
            }

            if (arg.Args[0] != "flash" && arg.Args[0] != "molly")
            {
                arg.ReplyWith("Wrong grenade type specified!");
                return;
            }

            BasePlayer target = Player.Find(arg.Args[1]);
            if(target == null)
            {
                arg.ReplyWith("Target not found!");
                return;
            }

            string nade = arg.Args[0] == "flash" ? config.flash.Name : config.molly.Name;
            DoPayAndGive(target, nade, true);
            arg.ReplyWith($"You gave {target.displayName} a {nade}");
        }


        private void cmdFlash(BasePlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "fancygrenades.useflash"))
            {
                Player.Reply(player, lang.GetMessage("NO_PERMISSION", this, player.UserIDString), config.settings.chatId);
                return;
            }
            DoPayAndGive(player, config.flash.Name, config.pay.costAmount == 0 ? true : false);
        }

        private void cmdMolly(BasePlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "fancygrenades.usemolly"))
            {
                Player.Reply(player, lang.GetMessage("NO_PERMISSION", this, player.UserIDString), config.settings.chatId);
                return;
            }
            DoPayAndGive(player, config.molly.Name, config.pay.costAmount == 0 ? true : false);
        }

        private void DoPayAndGive(BasePlayer player, string nadeType, bool free = false)
        {
            string paymentType = config.pay.costItem;

            if (config.pay.costItem.Equals("serverrewards", StringComparison.OrdinalIgnoreCase))
                paymentType = "RP";
            else if (config.pay.costItem.Equals("economics", StringComparison.OrdinalIgnoreCase))
                paymentType = "Economics";

            ulong skinId = nadeType == config.flash.Name ? config.flash.SkinId : config.molly.SkinId;
            Item nade = ItemManager.CreateByItemID(GrenadeId, 1, skinId);
            nade.name = nadeType == config.flash.Name ? config.flash.Name : config.molly.Name;
            string symbol = paymentType == "RP" ? "RP" : paymentType == "Economics" ? "$" : config.pay.costItem;

            if (!(HasFunds(player, paymentType) is bool) && !free)
            {               
                int amount = (int)HasFunds(player, paymentType);
                Player.Message(player, string.Format(lang.GetMessage("PURCHASE_FAILED", this, player.UserIDString), symbol, (config.pay.costAmount - amount), symbol));
                return;
            }
            if(!GiveItem(player, nade))
            {
                Player.Message(player, string.Format(lang.GetMessage("INVENTORY_FULL", this, player.UserIDString)));
                return;
            }
            else
            {
                if (!free)
                {
                    switch (paymentType)
                    {
                        case "RP":
                            ServerRewards?.CallHook("TakePoints", player.userID, config.pay.costAmount);
                            break;
                        case "Economics":
                            Economics?.CallHook("Withdraw", player.userID, (double)config.pay.costAmount);
                            break;
                        default:
                            player.inventory.Take(null, item.itemid, config.pay.costAmount);
                            break;
                    }
                    Player.Message(player, string.Format(lang.GetMessage("PURCHASE_SUCCESS", this, player.UserIDString), nade.name, config.pay.costAmount, symbol));
                    return;
                }
                Player.Message(player, string.Format(lang.GetMessage("GIVE_FREE", this, player.UserIDString), nade.name));
            }
        }

        object HasFunds(BasePlayer player, string paymentType)
        {
            if (paymentType == "RP")
            {
                int playerRP = (int)ServerRewards?.CallHook("CheckPoints", player.userID);
                if (playerRP >= config.pay.costAmount)
                    return true;

                return playerRP;
            }
            else if (paymentType == "Economics")
            {
                double playerEcon = (double)Economics?.CallHook("Balance", player.userID);
                if (playerEcon >= config.pay.costAmount)
                    return true;

                return playerEcon;               
            }
            else
            {
                if (GetItemAmount(player, item.shortname) >= config.pay.costAmount)
                    return true;

                return GetItemAmount(player, item.shortname);
            }
        }

        private bool GiveItem(BasePlayer player, Item i)
        {
            if (!player.inventory.GiveItem(i))
            {
                i.Remove();
                return false;
            }
            return true;
        }

        private bool InNadeAngle(BasePlayer player, BaseEntity ent)
        {
            float angle = Vector3.Angle((ent.transform.position - player.transform.position), player.eyes.HeadForward());
            if (angle < 99) return true;

            return false;
        }

        private int GetItemAmount(BasePlayer player, string shortname)
        {
            ItemDefinition item = ItemManager.FindItemDefinition(shortname);
            Item[] playerItems = player.inventory.AllItems();

            return playerItems.FirstOrDefault(x => x.info == item) == null ? 0 : playerItems.FirstOrDefault(x => x.info == item).amount;
        }

        private List<BasePlayer> GetPlayersInRadius(Vector3 pos, int radius)
        {
            var list = new List<BasePlayer>();
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (!player.userID.IsSteamId()) continue;
                if (Vector3.Distance(pos, player.transform.position) < radius)
                    list.Add(player);
            }
            return list;
        }

        private void FlashEffect(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "Flash");
            var Element = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = {Color = "1 1 1 1", FadeIn = 0.1f},
                            RectTransform = {AnchorMin = "0 0 ", AnchorMax = "1 1" },
                            CursorEnabled = false,
                            FadeOut = 2.5f
                        },
                        new CuiElement().Parent = "Overlay", "Flash"
                    }
                };
            CuiHelper.AddUi(player, Element);

            timer.Once(config.flash.Duration, () => CuiHelper.DestroyUi(player, "Flash"));
        }

        private void FireEffect(Vector3 pos)
        {
            var flame = GameManager.server.CreateEntity("assets/prefabs/ammo/rocket/rocket_fire.prefab", pos);
            flame?.Spawn();
        }
    }
}