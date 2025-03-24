/*
*  < ----- End-User License Agreement ----->
*  
*  You may not copy, modify, merge, publish, distribute, sublicense, or sell copies of this software without the developer’s consent.
*
*  THIS SOFTWARE IS PROVIDED BY IIIaKa AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, 
*  THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS 
*  BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE 
*  GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT 
*  LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*
*  Developer: IIIaKa
*      https://t.me/iiiaka
*      Discord: @iiiaka
*      https://github.com/IIIaKa
*      https://umod.org/user/IIIaKa
*      https://codefling.com/iiiaka
*      https://lone.design/vendor/iiiaka/
*      https://www.patreon.com/iiiaka
*      https://boosty.to/iiiaka
*  GitHub repository page: https://github.com/IIIaKa/FreeRT
*  
*  uMod plugin page: https://umod.org/plugins/free-rt
*  uMod license: https://umod.org/plugins/free-rt#license
*  
*  Codefling plugin page: https://codefling.com/plugins/free-rt
*  Codefling license: https://codefling.com/plugins/free-rt?tab=downloads_field_4
*  
*  Lone.Design plugin page: https://lone.design/product/free-rt/
*
*  Copyright © 2020-2024 IIIaKa
*/

using System;
using System.Collections.Generic;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("Free RT", "IIIaKa", "0.1.8")]
	[Description("A simple plugin that allows players with permissions to open card-locked doors in Rad Towns without a card.")]
	class FreeRT : RustPlugin
	{
		#region ~Variables~
		private const string PERMISSION_ALL = "freert.all", PERMISSION_GREEN = "freert.green", PERMISSION_BLUE = "freert.blue", PERMISSION_RED = "freert.red", Str_MsgNotAllowed = "MsgNotAllowed";
		#endregion
		
		#region ~Configuration~
		private static Configuration _config;

		private class Configuration
		{
			[JsonProperty(PropertyName = "Is it worth showing messages to players who don't have permissions?")]
			public bool ShowMessage = true;
			
			[JsonProperty(PropertyName = "Time in seconds(1-10) after which the door will close(hinged doors only)")]
            public float CloseTime = 5f;
			
			public Oxide.Core.VersionNumber Version;
		}
		
		protected override void LoadConfig()
        {
            base.LoadConfig();
            try { _config = Config.ReadObject<Configuration>(); }
            catch (Exception ex) { PrintError($"{ex.Message}\n\n[{Title}] Your configuration file contains an error."); }
            if (_config == null || _config.Version == new VersionNumber())
            {
                PrintWarning("The configuration file is not found or contains errors. Creating a new one...");
                LoadDefaultConfig();
            }
            else if (_config.Version < Version)
            {
				PrintWarning($"Your configuration file version({_config.Version}) is outdated. Updating it to {Version}.");
				_config.Version = Version;
				PrintWarning($"The configuration file has been successfully updated to version {_config.Version}!");
            }
			
			_config.CloseTime = Mathf.Clamp(_config.CloseTime, 1f, 10f);
			
			SaveConfig();
        }
		
		protected override void SaveConfig() => Config.WriteObject(_config);
		protected override void LoadDefaultConfig() => _config = new Configuration() { Version = Version };
		#endregion

		#region ~Language~
		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["MsgNotAllowed"] = "You do not have permission to open this door without the card!"
			}, this);
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["MsgNotAllowed"] = "У вас недостаточно прав для открытия этой двери без карточки!"
			}, this, "ru");
		}
        #endregion

        #region ~Methods~
		private bool TryOpenDoor(CardReader cardReader, BasePlayer player, Door door = null)
        {
			bool canOpen = false;
			if (permission.UserHasPermission(player.UserIDString, PERMISSION_ALL))
				canOpen = true;
            else
            {
                switch (cardReader.accessLevel)
                {
                    case 1:
                        if (permission.UserHasPermission(player.UserIDString, PERMISSION_GREEN))
                            canOpen = true;
                        break;
                    case 2:
                        if (permission.UserHasPermission(player.UserIDString, PERMISSION_BLUE))
                            canOpen = true;
                        break;
                    case 3:
                        if (permission.UserHasPermission(player.UserIDString, PERMISSION_RED))
                            canOpen = true;
                        break;
                    default:
                        break;
                }
            }

            if (canOpen)
			{
				if (door == null)
					cardReader.GrantCard();
				else
                {
					door.SetFlag(BaseEntity.Flags.Open, true);
                    timer.Once(_config.CloseTime, () =>
					{
						if (door != null && (cardReader == null || !cardReader.HasFlag(BaseEntity.Flags.On)))
							door.SetFlag(BaseEntity.Flags.Open, false);
					});
				}
			}
            else if (_config.ShowMessage)
                player.ChatMessage(lang.GetMessage(Str_MsgNotAllowed, this, player.UserIDString));
			return canOpen;
        }
		#endregion

		#region ~Oxide Hooks~
		void OnDoorKnocked(Door door, BasePlayer player)
		{
			if (door.isSecurityDoor && !door.IsOpen())
			{
				var crList = Pool.Get<List<CardReader>>();
				Vis.Entities(door.transform.position, 6f, crList);
				if (crList.Any())
					TryOpenDoor(crList[0], player, door);
				Pool.FreeUnmanaged(ref crList);
			}
		}
		
		void OnSwitchToggled(ElectricSwitch electricSwitch, BasePlayer player)
        {
			var crList = Pool.Get<List<CardReader>>();
            Vis.Entities(electricSwitch.transform.position, 2f, crList);
            if (crList.Any())
                TryOpenDoor(crList[0], player);
            Pool.FreeUnmanaged(ref crList);
        }
		
		void OnButtonPress(PressButton button, BasePlayer player)
        {
			CardReader cardReader;
			foreach (var input in button.inputs)
            {
				cardReader = input.connectedTo?.ioEnt as CardReader;
				if (cardReader != null)
				{
					TryOpenDoor(cardReader, player);
					break;
				}
			}
		}
		
		void Init()
        {
			Unsubscribe(nameof(OnDoorKnocked));
			Unsubscribe(nameof(OnSwitchToggled));
			Unsubscribe(nameof(OnButtonPress));
			permission.RegisterPermission(PERMISSION_ALL, this);
            permission.RegisterPermission(PERMISSION_GREEN, this);
            permission.RegisterPermission(PERMISSION_BLUE, this);
            permission.RegisterPermission(PERMISSION_RED, this);
		}
		
		void OnServerInitialized(bool initial)
		{
			Subscribe(nameof(OnDoorKnocked));
			Subscribe(nameof(OnSwitchToggled));
			Subscribe(nameof(OnButtonPress));
		}
		#endregion

		#region ~Unload~
		void Unload()
		{
			_config = null;
		}
		#endregion
	}
}