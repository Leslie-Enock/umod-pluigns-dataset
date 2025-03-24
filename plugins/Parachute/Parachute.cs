using System;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Parachute", "Rustoholics", "0.2.2")]
    [Description("Give players a parachute")]

    public class Parachute : CovalencePlugin
    {
        
        #region  Dependencies

        [PluginReference] private Plugin Chute;
        
        #endregion
        
        #region Config

        private double _fallSpeed = 1d;
        private float _fallTimer = 0.2f;
        private int _coolDown = 5;
        private float _packCheckTimer = 60f;
        private string _backpackPrefab = "assets/prefabs/misc/item drop/item_drop_backpack.prefab";

        private Dictionary<string, Timer> _fallTimers = new Dictionary<string, Timer>();

        private Configuration _config;
        protected override void SaveConfig() => Config.WriteObject(_config);
        protected override void LoadDefaultConfig() => _config = new Configuration();

        private class Configuration
        {
            public bool ShowBackpack = true;
            public ulong ParachuteSkinId = 1901976770;

            public string ParachuteName = "Parachute";

            public string ParachuteShortname = "attire.hide.poncho";
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
        #endregion
        
        #region Language
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["InvalidPlayer"] = "Invalid player",
                ["ParachuteGiveTo"] = "Parachute given to {0}",
            }, this);
        }
        
        #endregion

        # region Hooks
        private void OnServerInitialized()
        {
            permission.RegisterPermission("parachute.admin", this);
            
            foreach (var p in players.Connected)
            {
                MonitorPlayerFall(p.Object as BasePlayer);
                WearSetupAndTimer(p.Object as BasePlayer);
            }
        }
        
        private void OnPlayerConnected(BasePlayer player)
        {
            MonitorPlayerFall(player);
            WearSetupAndTimer(player);
        }

        private void WearSetupAndTimer(BasePlayer player)
        {
            WearParachutePack(player);
        }
        
        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (info != null && info.HitEntity != null && info.HitEntity.ToPlayer() != null && info.HitEntity.ToPlayer().userID.IsSteamId())
            {
                if (info.damageTypes.Has(DamageType.Fall) && IsWearingParachute(info.HitEntity.ToPlayer()))
                {
                    return true;
                }
            }
            return null;
        }
        
        private void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            DoPackWearing(container.playerOwner, item);
        }
        
        private void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            DoPackWearing(container.playerOwner, item);
        }
        
        #endregion

        #region Functions

        private void MonitorPlayerFall(BasePlayer player)
        {
            if (Chute == null || !Chute.IsLoaded) return;
            
            if (_fallTimers.ContainsKey(player.UserIDString))
            {
                _fallTimers[player.UserIDString].Destroy();
            }
            
            var d = 0d;
            DateTime lastChute = default(DateTime);
            
            _fallTimers[player.UserIDString] = timer.Every(_fallTimer, () =>
            {
                
                if (d > 0 && !player.IsFlying && d - player.transform.position.y > _fallSpeed)
                {
                    
                    if (IsWearingParachute(player)
                        && !player.isMounted
                        && (default(DateTime) == lastChute || lastChute.AddSeconds(_coolDown) < DateTime.Now)
                        && !Chute.Call<bool>("ActiveChutePlayerList", player) 
                        && Chute.Call<bool>("IsAboveGround", player))
                    {
                        Chute.Call("ExternalAddPlayerChute", player, "");
                        lastChute = DateTime.Now;
                    }
                }
                d = player.transform.position.y;
            });
        }
        
        private bool IsWearingParachute(BasePlayer player)
        {
            foreach (var item in player.inventory.containerWear.itemList)
            {
                if (ItemIsParachute(item)) return true;
            }

            return false;
        }

        private bool ItemIsParachute(Item item)
        {
            if (item == null) return false;
            return item.info.shortname == _config.ParachuteShortname && item.skin == _config.ParachuteSkinId;
        }
        
        private void WearParachutePack(BasePlayer player)
        {
            if (!_config.ShowBackpack || !IsWearingParachute(player)) return;
            
            UnwearParachutePack(player);
            
            var parachute = GameManager.server.CreateEntity(_backpackPrefab, new Vector3(), new Quaternion(-3f, 0f, 3f, 1f), false) ;
            parachute.SetParent(player, "spine3");
            parachute?.Spawn();

        }
        
        private void UnwearParachutePack(BasePlayer player)
        {
            for (var x = player.children.Count-1; x >= 0;x--)
            {
                if (player.children[x].PrefabName == _backpackPrefab)
                {
                    player.children[x].parentBone = 0; 
                    player.children[x].Kill();
                }
            }
        }

        private bool? OnEntityKill(BaseNetworkable entity)
        {
            if (entity != null && entity.PrefabName != null && entity.PrefabName == _backpackPrefab)
            {
                var e = (entity as BaseEntity);
                if (e != null && e.parentBone > 0)
                {
                    return false;
                }
            }

            return null;
        }

        private void DoPackWearing(BasePlayer player, Item item)
        {
            if (!_config.ShowBackpack) return;
            if (!ItemIsParachute(item) || player == null || !player.userID.IsSteamId()) return;
            
            if (IsWearingParachute(player))
            {
                WearParachutePack(player);
            }
            else
            {
                UnwearParachutePack(player);
                if (player.isMounted && Chute != null && Chute.IsLoaded)
                {
                    player.GetMounted().Kill();
                    Chute.Call("RemovePlayerID", player);
                }

            }
        }
        
        private void GiveParachute(BasePlayer player)
        {

            var itemDefinition = ItemManager.FindItemDefinition(_config.ParachuteShortname);
            if (itemDefinition == null) return;
            var item = ItemManager.Create(itemDefinition, 1, _config.ParachuteSkinId);
            item.name = _config.ParachuteName;
            
            var wearAvailable = player.inventory.containerWear.capacity - player.inventory.containerWear.itemList.Count;
            var beltAvailable = player.inventory.containerBelt.capacity - player.inventory.containerBelt.itemList.Count;
            if (wearAvailable > 0)
            {
                item.MoveToContainer(player.inventory.containerWear);
                return;
            }
            if (beltAvailable > 0)
            {
                item.MoveToContainer(player.inventory.containerBelt);
                return;
            }
            player.inventory.GiveItem(item);
            
        }
        #endregion

        #region Commands
        [Command("parachute.give"), Permission("parachute.admin")]
        private void GivePackCommand(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player;
            if (args == null || args.Length == 0)
            {
                player = iplayer.Object as BasePlayer;
            }
            else
            {
                ulong playerid;
                if (UInt64.TryParse(args[0], out playerid)){
                    player = BasePlayer.FindByID(playerid);
                }
                else
                {
                    player = null;
                }
            }

            if (player == null)
            {
                iplayer.Reply(Lang("InvalidPlayer", iplayer.Id));
                return;
            }

            GiveParachute(player);

            iplayer.Reply(Lang("ParachuteGiveTo", iplayer.Id, player.displayName));

        }
        #endregion
    }
}