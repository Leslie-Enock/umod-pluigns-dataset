
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Roulette Broadcast", "supreme", "1.2.0")]
    [Description("Broadcasts the payout on the roulette")]
    public class RouletteBroadcast : RustPlugin
    {
        
        #region Configuration

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Amount of scrap to win in order to broadcast")]
            public int amount = 300;
            
            [JsonProperty(PropertyName = "Broadcast Icon (SteamID)")]
            public ulong chatIcon = 0;
            
            [JsonProperty(PropertyName = "Enable Broadcast Delay")]
            public bool delay = true;
            
            [JsonProperty(PropertyName = "Broadcast Delay Time (seconds)")]
            public int delayTime = 30;
            
            [JsonProperty(PropertyName = "Gametip Message")]
            public bool gameTip = true;
            
            [JsonProperty(PropertyName = "Gametip Message Time")]
            public float gameTipTime = 5f;
            
            [JsonProperty(PropertyName = "Custom Rewards")]
            public bool enableRewards = true;
            
            [JsonProperty(PropertyName = "Custom Rewards Message")]
            public bool enableRewardsMessage = true;

            [JsonProperty(PropertyName = "Custom Rewards Items")]
            public Dictionary<string, int> Items;
            
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration
            {
                Items = new Dictionary<string, int>
                {
                    {"rock", 1},
                    {"torch", 1},
                    {"stones", 500},
                    {"rifle.ak", 1}
                }
            };
        }

        #endregion
        
        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Broadcast Message"] = "<color=#e3e3e3><color=#ACFA58>{0}</color> just won <color=#ACFA58>{1}</color> scrap on the roulette!</color>",
                ["Custom Rewards"] = "<color=#e3e3e3>You have been <color=#ACFA58>rewarded</color> with custom items!</color>"
            }, this);
        }
        
        #endregion

        private void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            BigWheelBettingTerminal entity = container.entityOwner as BigWheelBettingTerminal;
            if (entity == null) return;
            if (!entity.inventory.IsLocked()) return;
            BasePlayer seatedPlayer = null;
            List<BasePlayer> nearbyPlayers = Facepunch.Pool.GetList<BasePlayer>();
            Vis.Entities(container.entityOwner.transform.position, entity.offsetCheckRadius + 1f, nearbyPlayers);
            Vector3 offset = entity.transform.TransformPoint(entity.seatedPlayerOffset);
            foreach (BasePlayer player in nearbyPlayers)
            {
                if (!player.isMounted) continue;
                if (Vector3Ex.Distance2D(player.transform.position, offset) <= entity.offsetCheckRadius)
                {
                    seatedPlayer = player;
                    break;
                }
            }
            Facepunch.Pool.FreeList(ref nearbyPlayers);
            if (seatedPlayer == null) return;
            if (item.amount >= _config.amount)
            {
                if (_config.delay)
                {
                    timer.In(_config.delayTime, () => Server.Broadcast(Lang("Broadcast Message", null, seatedPlayer.displayName, item.amount), _config.chatIcon));
                }
                else
                {
                    Server.Broadcast(Lang("Broadcast Message", null, seatedPlayer.displayName, item.amount), _config.chatIcon);
                }
                
                if (_config.enableRewards)
                {
                    foreach (var i in _config.Items)
                    {
                        var give = ItemManager.CreateByName(i.Key, i.Value);
                        if (give == null) continue;
                        seatedPlayer.GiveItem(give);
                    }
                    if (_config.enableRewardsMessage) seatedPlayer.ChatMessage(Lang("Custom Rewards", seatedPlayer.UserIDString));
                    if (_config.gameTip)
                    {
                        seatedPlayer.SendConsoleCommand("gametip.showgametip", Lang("Custom Rewards", seatedPlayer.UserIDString));
                        timer.In(_config.gameTipTime, () => seatedPlayer.Command("gametip.hidegametip"));
                    }
                }
            }
        }

        #region Helpers

        string Lang(string key, string id, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion
    }
}