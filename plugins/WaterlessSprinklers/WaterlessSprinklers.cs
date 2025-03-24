using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Linq;
//
// rev 0.1.0
// rev 0.2.0
//   change missing space in title from code review.
// rev 0.3.0
//   change to void OnEntitySpawned(Sprinkler sprinkler) to avoid unnecessary hook calls
// rev 1.0.0
//     remove sprinkler.prefabID == 2389629329 check, not needed anymore
// rev 1.0.1
//     Delay sprinkler init by 1 sec.
// rev 1.0.2
//     Option to ignore sprinklers with conections
// rev 1.0.3
//     if sprinkler is assign a skinid , it will not need water

namespace Oxide.Plugins
{
    [Info("Waterless Sprinklers", "Lorenzo", "1.0.3")]
    [Description("Sprinkers dont need water to run")]
    class WaterlessSprinklers : CovalencePlugin
    {
        private ItemDefinition water = ItemManager.FindItemDefinition("water");

        private const int sprinklerID = -781014061;
        private const ulong sprinklerSkinID = 234508;

        #region Configuration

        private static Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Use permission")]
            public bool UsePermission = false;      // use permission or grant access to every players

            [JsonProperty(PropertyName = "Permission")]
            public string PermissionUse = "waterlesssprinklers.use";   // name of permission

            [JsonProperty(PropertyName = "PermissionAdmin")]
            public string PermissionAdmin = "waterlesssprinklers.admin";   // name of permission

            [JsonProperty(PropertyName = "Make sprinkler special command")]
            public string setskincommand = "setsprinklerspecial";

            [JsonProperty(PropertyName = "Only turn on if there is no IO connected to sprinklers")]
            public bool checkIOconnection = false;      // use permission or grant access to every players
        };

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig() => _config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion Configuration

        #region Variables
        #endregion

        #region Hooks Targets
        //
        private void Unload()
        {
            UpdateAllSprinklers(false);
        }

        private void OnServerInitialized()
        {
            permission.RegisterPermission(_config.PermissionUse, this);
            permission.RegisterPermission(_config.PermissionAdmin, this);

            AddCovalenceCommand(_config.setskincommand, nameof(SprinklerSetSkin));

            timer.Once(1f, () =>
            {
                UpdateAllSprinklers(true);
            });            
        }

        void OnEntitySpawned(Sprinkler sprinkler)
        {
            if (CanUse(sprinkler.OwnerID) || sprinkler.skinID == sprinklerSkinID)
            {
                sprinkler.SetFuelType(water, null);
                sprinkler.SetSprinklerState(true);
            }
        }

        #endregion Hooks

        #region Commands
        private void SprinklerSetSkin(IPlayer iplayer, string command, string[] args)
        {
            var player = iplayer.Object as BasePlayer;
            if (player != null && IsAdmin(player.userID))
            {
                Item item = player.inventory.containerMain.FindItemByItemID(sprinklerID);
                if (item==null) item = player.inventory.containerBelt.FindItemByItemID(sprinklerID);

                if (item != null)
                {
                    item.skin = sprinklerSkinID;
                    SendChatMessage(player, "Converted Sprinkler");
                }
                else SendChatMessage(player, "Sprinkler not found");
            }
            else SendChatMessage(player, "You do not have permission");

            return;
        }
        #endregion Commands


        public void SendChatMessage(BasePlayer player, string msg)
        {
            player.ChatMessage(msg);
        }

        void UpdateAllSprinklers(bool state)
        {
            foreach (var sprinkler in BaseNetworkable.serverEntities.OfType<Sprinkler>())
            {
                if ((_config.checkIOconnection && sprinkler.inputs[0].connectedTo.ioEnt == null && sprinkler.outputs[0].connectedTo.ioEnt == null) || !_config.checkIOconnection)
                {
                    if (CanUse(sprinkler.OwnerID) || sprinkler.skinID == sprinklerSkinID)
                    {
                        sprinkler.SetFuelType(water, null);
                        sprinkler.SetSprinklerState(state);
                    }
                }
            }
        }

        private bool CanUse(ulong id) => (!_config.UsePermission || permission.UserHasPermission(id.ToString(), _config.PermissionUse));

        private bool IsAdmin(ulong id) => (permission.UserHasPermission(id.ToString(), _config.PermissionAdmin));
    }
}