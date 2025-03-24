using System;
using System.Collections.Generic;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Loot Snitch", "August", "1.0.7")]
    [Description("Players with permission get messaged who/when a player is looting their body.")]
    
    public class LootSnitch : RustPlugin
    {
        #region Initialization
        
        private const string PermUse = "lootsnitch.use";
        private const string PermOverride = "lootsnitch.override";
        private const string PermMangage = "lootsnitch.manage";
        private bool IsEnabled = true;
        private int Cooldown = 60;

        void Init()
        {
            permission.RegisterPermission(PermUse, this);
            permission.RegisterPermission(PermOverride, this);
            permission.RegisterPermission(PermMangage, this);
            
            try
            {
                data = Interface.Oxide.DataFileSystem.ReadObject<Data>(Name);
            }
            catch { }

            if (data == null)
                data = new Data();

            if (Cooldown <= 0)
                data.Cooldowns.Clear();
        }
        
        protected override void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string> {
                ["Snitch Message"] = "{0} just looted your body!",

                ["Enabled"] = "Loot Snitcher is now Enabled.",
                ["Disabled"] = "Loot Snitcher is now Disabled.",
                
                ["NoPermission"] = "Error: No Permission",
                ["Syntax"] = "Error: Syntax",
                
                ["NewCooldown"] = "The cooldown is now {0} seconds."
            }, this);
        }
        #endregion
        
        #region Cooldown (by nivex)
        private static Data data = new Data();
        private class Data
        {
            public Dictionary<string, long> Cooldowns = new Dictionary<string, long>();
            
        }
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1);
        public uint GetUnixTimestamp() => (uint)DateTime.UtcNow.Subtract(Epoch).TotalSeconds;
        #endregion
        
        #region Hooks and Commands
        private void OnLootEntityEnd(BasePlayer player, LootableCorpse corpse)
        {
            var target = BasePlayer.FindByID(corpse.playerSteamID);

            if (!target)
            {
                return;
            }
            
            if (permission.UserHasPermission(player.UserIDString, PermOverride))
            {
                return;
            }
            if (!permission.UserHasPermission(target.UserIDString, PermUse))
            {
                return;
            }

            if (Cooldown > 0 && data.Cooldowns.ContainsKey(player.UserIDString))
            {
                if (data.Cooldowns[target.UserIDString] - GetUnixTimestamp() > 0)
                {
                    return;
                }
                data.Cooldowns.Remove(target.UserIDString);
            }

            if (Cooldown > 0 && !data.Cooldowns.ContainsKey(target.UserIDString))
            {
                data.Cooldowns.Add(target.UserIDString, GetUnixTimestamp() + Cooldown);
            }
            
            target.ChatMessage(Lang("Snitch Message", target.UserIDString, player.displayName));
        }

        [ChatCommand("snitch")]
        private void SnitchChatCommand(BasePlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermMangage))
            {
                player.ChatMessage(Lang("NoPermission", player.UserIDString));
                return;
            }
            if (args.Length != 1)
            {
                player.ChatMessage(Lang("Syntax", player.UserIDString));
                return;
            }

            switch (args[0].ToLower())
            {
                case "toggle":
                    
                    ToggleSnitch(player);
                    break;
                
                default:
                    int cooldown;

                    if (int.TryParse(args[0], out cooldown))
                    {
                        Cooldown = cooldown;
                        player.ChatMessage(Lang("NewCooldown", player.UserIDString, Cooldown));
                        break;
                    }

                    player.ChatMessage(Lang("Syntax", player.UserIDString));;
                    return;
            }
        }
        #endregion
        
        #region Helpers and Functions
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private void ToggleSnitch(BasePlayer player)
        {
            if (IsEnabled == true)
            {
                player.ChatMessage(Lang("Disabled", player.UserIDString));
                Unsubscribe(nameof(OnLootEntityEnd));
            }
            else
            {
                player.ChatMessage(Lang("Enabled", player.UserIDString));
                Subscribe(nameof(OnLootEntityEnd));
            }
            IsEnabled = !IsEnabled;
        }       
        #endregion

    }
}