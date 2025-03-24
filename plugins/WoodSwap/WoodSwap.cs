using System.Collections.Generic;
using Rust;
using System;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Wood Swap", "The Friendly Chap", "1.0.4")]
    [Description("Instanty burns wood into charcoal on command")]
    public class WoodSwap : RustPlugin
	
/*	MIT License

	©2024 The Friendly Chap

	Permission is hereby granted, free of charge, to any person obtaining a copy
	of this software and associated documentation files (the "Software"), to deal
	in the Software without restriction, including without limitation the rights
	to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
	copies of the Software, and to permit persons to whom the Software is
	furnished to do so, subject to the following conditions:

	The above copyright notice and this permission notice shall be included in all
	copies or substantial portions of the Software.

	THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
	IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
	FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
	AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
	LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
	OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
	SOFTWARE.
*/	
	
    {
        #region Variables
        
        private const string permUse = "woodswap.use";
        
        #endregion

        #region Oxide Hooks
        
        private void Init()
        {
            permission.RegisterPermission(permUse, this);
            cmd.AddChatCommand(config.command, this, nameof(cmdSwapChat));
            cmd.AddConsoleCommand(config.command, this, nameof(cmdSwapConsole));
			// ShowLogo();
        }
        
        #endregion

        #region Commands
        
        private void cmdSwapConsole(ConsoleSystem.Arg arg)
        {
            SwapWood(arg.Player());
        }

        private void cmdSwapChat(BasePlayer player)
        {
            SwapWood(player);
        }

        #endregion

        #region Core

        private void SwapWood(BasePlayer player)
        {
            var woodItemList = player.inventory.AllItems().Where(x => x.info.shortname == "wood").ToList();
            if (woodItemList.Count == 0)
            {
                Message(player, "No Wood");
                return;
            }
            
            var countWood = woodItemList.Sum(x => x.amount);
            var countCharcoal = Convert.ToInt32(countWood * config.rate);
            var charcoal = ItemManager.CreateByName("charcoal", countCharcoal);
            
            foreach (var wood in woodItemList)
            {
                wood.GetHeldEntity()?.Kill();
                wood.Remove();
            }
            
            player.GiveItem(charcoal);
            Message(player, "Swap Success", countWood, countCharcoal);
        }
		
				private void ShowLogo()
        {
			Puts(" _______ __               _______        __                 __ __             ______ __           ©2024");
			Puts("|_     _|  |--.-----.    |    ___|.----.|__|.-----.-----.--|  |  |.--.--.    |      |  |--.---.-.-----.");
			Puts("  |   | |     |  -__|    |    ___||   _||  ||  -__|     |  _  |  ||  |  |    |   ---|     |  _  |  _  |");
			Puts("  |___| |__|__|_____|    |___|    |__|  |__||_____|__|__|_____|__||___  |    |______|__|__|___._|   __|");
			Puts("                                 Wood Swap v1.0.3                 |_____| thefriendlychap.co.za |__|");      
        }

        #endregion

        #region Configuration 1.1.0

        private static ConfigData config;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Command")]
            public string command;

            [JsonProperty(PropertyName = "Rate")]
            public float rate;
        }

        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                command = "char",
                rate = 1.1f
            };
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
                PrintError("Configuration file is corrupt! Unloading plugin...");
                Interface.Oxide.RootPluginManager.RemovePlugin(this);
                return;
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion
        
        #region Localization 1.1.1
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"No Wood", "You don't have wood."},
                {"Swap Success", "You successfully burned wood x{0} into charcoal x{1}"},
            }, this);
        }

        private void Message(BasePlayer player, string messageKey, params object[] args)
        {
            if (player == null)
            {
                return;
            }

            var message = GetMessage(messageKey, player.UserIDString, args);
            player.SendConsoleCommand("chat.add", (object) 0, (object) message);
        }

        private string GetMessage(string messageKey, string playerID, params object[] args)
        {
            return string.Format(lang.GetMessage(messageKey, this, playerID), args);
        }

        #endregion
    }
}