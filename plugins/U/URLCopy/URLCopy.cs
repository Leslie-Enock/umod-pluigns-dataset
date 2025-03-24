using Newtonsoft.Json;
using Oxide.Core;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("URL Copy", "PaiN", "0.1.0")]
    [Description("Gives a player a note with a set message.")]
    class URLCopy : RustPlugin
    {
        private Configuration config;
        const int NoteID = 1414245162;

        private Dictionary<string, float> cmdCD = new Dictionary<string, float>();

        private class Configuration
        {
            [JsonProperty("Command || Note Text")]
            public Dictionary<string, string> cmds = new Dictionary<string, string>
            {
                ["umod"] = "To copy the link, mark it with your mouse and then press Ctrl+C on your keyboard \n www.umod.org",
                ["discord"] = "To copy the link, mark it with your mouse and then press Ctrl+C on your keyboard \n DiscordLinkHere"
            };

            [JsonProperty("Command usage cooldown in seconds (0 = Disabled)")]
            public int cooldown = 60;

            [JsonProperty("Message SteamID icon")]
            public ulong chatId = 0;

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
            foreach (var command in config.cmds.Keys)
                cmd.AddChatCommand(command, this, HandleNote);

            permission.RegisterPermission("urlcopy.use", this);
        }

        private void HandleNote(BasePlayer player, string cmd, string[] args)
        {
            if(!permission.UserHasPermission(player.UserIDString, "urlcopy.use"))
            {
                Player.Reply(player,
                    lang.GetMessage("NO_PERMISSION", this, player.UserIDString),
                    config.chatId);
                return;
            }

            if (OnCooldown(player))
            {
                Player.Reply(player,
                    string.Format(lang.GetMessage("CMD_COOLDOWN", this, player.UserIDString),
                    GetCooldown(player.UserIDString),
                    config.chatId));
                return;
            }

            Item note = ItemManager.CreateByItemID(NoteID);
            note.text = config.cmds.FirstOrDefault(x => x.Key == cmd).Value;

            if(!player.inventory.GiveItem(note))                
            {
                note.Remove();
                Player.Reply(player, lang.GetMessage("INVENTORY_FULL", this, player.UserIDString), config.chatId);
                return;
            }

            player.inventory.GiveItem(note);
            Player.Reply(player, lang.GetMessage("NOTE_RECEIVED", this, player.UserIDString), config.chatId);
            
            if (config.cooldown > 0)
            {
                cmdCD.Remove(player.UserIDString);
                cmdCD.Add(player.UserIDString, Time.realtimeSinceStartup);
            }
        }

        private bool OnCooldown(BasePlayer player)
        {
            if (config.cooldown == 0) return false;
            if(cmdCD.ContainsKey(player.UserIDString))
            {
                if ((Time.realtimeSinceStartup - cmdCD[player.UserIDString]) < config.cooldown)
                    return true;
            }
            return false;
        }

        private int GetCooldown(string userid) => (int)((config.cooldown + cmdCD[userid]) - Time.realtimeSinceStartup);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NOTE_RECEIVED"] = "You have received a note! Check your inventory.",
                ["INVENTORY_FULL"] = "You don't have enough space in your inventory!",
                ["NO_PERMISSION"] = "You do not have permission to use this command!",
                ["CMD_COOLDOWN"] = "You are on cooldown! {0}s remaining"
            }, this);
        }
    }
}