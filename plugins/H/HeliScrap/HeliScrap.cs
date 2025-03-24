using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using System;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Heli Scrap", "Camoec", "1.5.1")]
    [Description("Call heli with scrap")]

    public class HeliScrap : RustPlugin
    {
        private PluginConfig _config;
        [PluginReference]
        private readonly Plugin Economics;

        private class PluginConfig
        {
            [JsonProperty(PropertyName = "ChatPrefix")]
            public string ChatPrefix = "<color=#eb4213>HeliScrap</color>:";

            [JsonProperty(PropertyName = "Command")]
            public string Command = "CallHeli";

            [JsonProperty(PropertyName = "Scrap Amount (If Economics is enabled, use RP)")]
            public int ScrapAmount = 100;

            [JsonProperty(PropertyName = "UseEconomics")]
            public bool UseEconomics = false;

            [JsonProperty(PropertyName = "UsePermission")]
            public bool UsePermission = true;

            [JsonProperty(PropertyName = "MaxSpawnedHelis")]
            public int MaxSpawnedHelis = 1;

            [JsonProperty(PropertyName = "Player Cooldown (in seconds)")]
            public int Cooldown = 60;

            [JsonProperty(PropertyName = "Global Cooldown (in seconds)")]
            public int GCooldown = 30;
        }

        private const string UsePerm = "heliscrap.use";
        private const string HELI_PREFAB = "assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab";
        private HashSet<PatrolHelicopter> activeHelis = new HashSet<PatrolHelicopter>();
        private Dictionary<BasePlayer, DateTime> playerCooldown = new Dictionary<BasePlayer, DateTime>();
        private DateTime lastCall = new DateTime();


        private bool RemoveCost(BasePlayer player)
        {
            if(_config.UseEconomics)
            {
                var ret = (bool)Economics.Call("Withdraw", player.userID, (double)_config.ScrapAmount);
                
                return ret;
            }
            else if(CanRemoveItem(player, -932201673, _config.ScrapAmount))
            {
                RemoveItemsFromInventory(player, -932201673, _config.ScrapAmount);
                return true;
            }
            return false;
        }

        #region Config Setup

        protected override void SaveConfig() => Config.WriteObject(_config, true);
        protected override void LoadDefaultConfig()
        {
            //base.LoadDefaultConfig();
            _config = new PluginConfig();
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<PluginConfig>();

                if (_config == null)
                    throw new Exception();

                SaveConfig(); // override posible obsolet / outdated config
            }
            catch (Exception)
            {
                PrintError("Loaded default config.");

                LoadDefaultConfig();
            }

            
        }

        void Loaded()
        {
            if (_config.UseEconomics == true && Economics == null)
            {
                Puts("Economics not found, disabling feature");
                _config.UseEconomics = false;
                return;
            }
            if(Economics != null)
            {
                Puts("Economics Found!");
            }
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Success"] = "Heli called successfuly",
                ["MaxSpawnedHelis"] = "Max heli in bound reached",
                ["NoRequiredScrap"] = "You don't have {0} of scrap in your inventory",
                ["NoPermission"] = "You don't have required permission to use this command",
                ["Cooldown"] = "You need to wait {0} seconds to use this command",
                ["NoRequiredRP"] = "You don't have {0} of RP"
            }, this);
        }

        private string Lang(string key, string userid) => lang.GetMessage(key, this, userid);

        #endregion

        private void Init()
        {
            cmd.AddChatCommand(_config.Command, this, "CallCommand");
            permission.RegisterPermission(UsePerm, this);
        }

        private void CallCommand(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, UsePerm) && _config.UsePermission)
            {
                PrintToChat(player, $"{_config.ChatPrefix} {Lang("NoPermission", player.UserIDString)}");
                return;
            }


            if (DateTime.Now - lastCall < TimeSpan.FromSeconds(_config.GCooldown))
            {
                PrintToChat(player, string.Format(Lang("Cooldown", player.UserIDString), _config.GCooldown - (int)(DateTime.Now - lastCall).TotalSeconds));
                return;
            }



            if (playerCooldown.ContainsKey(player) && DateTime.Now - playerCooldown[player] < TimeSpan.FromSeconds(_config.Cooldown))
            {
                PrintToChat(player, string.Format(Lang("Cooldown", player.UserIDString), _config.Cooldown - (int)(DateTime.Now - playerCooldown[player]).TotalSeconds));
                return;
            }


            CheckHelis();
            if(activeHelis.Count < _config.MaxSpawnedHelis)
            {
                if(RemoveCost(player))
                {
                    callHeli(player.transform.position, false);


                    if (playerCooldown.ContainsKey(player))
                    {
                        playerCooldown[player] = DateTime.Now;
                    }
                    else
                    {
                        playerCooldown.Add(player, DateTime.Now);
                    }
                    lastCall = DateTime.Now;


                    PrintToChat(player, $"{_config.ChatPrefix} {Lang("Success", player.UserIDString)}");
                }
                else
                {
                    if (_config.UseEconomics)
                    {
                        PrintToChat(player, $"{_config.ChatPrefix} {string.Format(Lang("NoRequiredRP", player.UserIDString), _config.ScrapAmount)}");
                    }
                    else
                    {
                        PrintToChat(player, $"{_config.ChatPrefix} {string.Format(Lang("NoRequiredScrap", player.UserIDString), _config.ScrapAmount)}");
                    }
                }
            }
            else
            {
                PrintToChat(player, $"{_config.ChatPrefix} {Lang("MaxSpawnedHelis", player.UserIDString)}");
            }
        }

        
        

        private void OnServerInitialized()
        {
            
            foreach(var entity in BaseNetworkable.serverEntities)
            {
                if (entity == null || (entity as PatrolHelicopter) == null)
                    continue;

                activeHelis.Add(entity as PatrolHelicopter);
            }
        }

        void OnEntitySpawned(PatrolHelicopter entity)
        {
            if (entity != null)
                activeHelis.Add(entity);
        }

        

        #region Misc

        private void CheckHelis()
        {
            activeHelis.RemoveWhere(heli => heli?.IsDestroyed ?? true);
        }

        private bool CanRemoveItem(BasePlayer player, int itemid, int amount)
        {
            var foundAmount = 0;
            foreach (var item in player.inventory.containerMain.itemList)
            {
                if (item != null && item.info.itemid == itemid)
                {
                    foundAmount = foundAmount + item.amount;
                }
            }

            if (foundAmount >= amount)
                return true;
            return false;
        }

        public void RemoveItemsFromInventory(BasePlayer player, int itemid, int amount)
        {
            var items = player.inventory.containerMain.itemList;

            int removeAmount = 0;
            int amountRemaining = amount;

            for(int i = 0; i < items.Count; i++ )
            {
                var item = items[i];
                if (item == null || item.info.itemid != itemid)
                    continue;

                removeAmount = amountRemaining;
                if (item.amount < removeAmount)
                    removeAmount = item.amount;

                if (item.amount > removeAmount)
                    item.SplitItem(removeAmount);
                else
                    item.UseItem(removeAmount);
                amountRemaining = amountRemaining - removeAmount;

                if (amountRemaining <= 0)
                    break;
            }
        }
        private PatrolHelicopter callHeli(Vector3 coordinates = new Vector3(), bool setPositionAfterSpawn = true)
        {
            var heli = (PatrolHelicopter)GameManager.server.CreateEntity(HELI_PREFAB, new Vector3(), new Quaternion(), true);
            if (heli == null)
            {
                PrintWarning("Failed to create heli prefab on " + nameof(callHeli));
                return null;
            }

            var heliAI = heli?.GetComponent<PatrolHelicopterAI>() ?? null;
            if (heliAI == null)
            {
                PrintWarning("Failed to get helicopter AI on " + nameof(callHeli));
                return null;
            }
            if (coordinates != Vector3.zero)
            {
                if (coordinates.y < 225)
                    coordinates.y = 225;
                heliAI.SetInitialDestination(coordinates, 0.25f);
                if (setPositionAfterSpawn)
                    heli.transform.position = heliAI.transform.position = coordinates;
            }
            heli.Spawn();
            
            return heli;
        }

        #endregion

    }
}