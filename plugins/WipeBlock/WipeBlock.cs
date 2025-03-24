using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using Rust;
using UnityEngine;
using Random = Oxide.Core.Random;

namespace Oxide.Plugins
{
    [Info("Wipe Block", "Orange", "1.0.6")]
    [Description("Block items for selected time after wipe")]
    public class WipeBlock : RustPlugin
    {
        #region Vars

        private const string elem = "wipeblock.panel";

        #endregion
        
        #region Oxide Hooks

        private void Init()
        {
            foreach (var entity in UnityEngine.Object.FindObjectsOfType<AutoTurret>())
            {
                OnEntitySpawned(entity);
            } 
        } 

        private void OnEntitySpawned(AutoTurret turret)
        {
            if (turret is NPCAutoTurret || turret.OwnerID.IsSteamId() == false)
            {
                return;
            }
            
            NextTick(() =>
            {
                if (turret.IsValid() == true && turret.inventory != null)
                {
                    turret.inventory.onItemAddedRemoved += (item, b) => OnAddedItemInTurret(turret, item, b);
                }
            });
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList.ToList())
            {
                CuiHelper.DestroyUi(player, elem);
            }
        }
        
        private object CanEquipItem(PlayerInventory inventory, Item item, int targetPos)
        {
            return CanWearItem(inventory, item, targetPos);
        }
        
        private object OnWeaponReload(BaseProjectile projectile, BasePlayer player)
        {
            return OnMagazineReload(projectile, -1, player);
        }

        private object CanWearItem(PlayerInventory inventory, Item item, int targetPos)
        {
            var flag = CanUseItem(inventory.GetComponent<BasePlayer>(), item.info.shortname);
            return flag ? (object) null : false;
        }
        
        private object OnMagazineReload(BaseProjectile projectile, int desiredAmount, BasePlayer player)
        {
            if (projectile.primaryMagazine.definition.ammoTypes == AmmoTypes.RIFLE_556MM)
            {
                NextTick(()=> {CheckGun(player, projectile);});
            }

            var flag = CanUseItem(player, projectile.primaryMagazine.ammoType.shortname);
            return flag ? (object) null : true;
        }

        #endregion

        #region Core

        private void OnAddedItemInTurret(AutoTurret turret, Item item, bool added)
        {
            if (added == false)
            {
                return;
            }

            if (item.parent == null)
            {
                return;
            }

            if (IsBlocked(item.info.shortname) == false)
            {
                return;
            } 
            
            item.Drop(turret.transform.position + new Vector3(0, 1, 0), turret.GetDropVelocity());
        }

        private bool CanUseItem(BasePlayer player, string shortName)
        {
            if (player.IsAdmin == true || player.userID.IsSteamId() == false)
            {
                return true;
            }

            if (config.items.ContainsKey(shortName) == false)
            {
                return true;
            }

            var blockLeft = GetBlockTime(shortName);
            if (blockLeft > 0)
            {
                var time = GetTimeString(blockLeft);
                ShowUI(player, time);
                Message(player, "Item Blocked", time);
                return false;
            }
            
            config.items.Remove(shortName);
            return true;
        }

        private int GetBlockTime(string shortname)
        {
            if (config.items.ContainsKey(shortname) == false)
            {
                return 0;
            }

            var blockLeft = config.items[shortname] - PassedSinceWipe();
            return blockLeft;
        }

        private bool IsBlocked(string shortname)
        {
            return GetBlockTime(shortname) > 0;
        }

        private void CheckGun(BasePlayer player, BaseProjectile weapon)
        {
            var magazine = weapon.primaryMagazine;
            if (magazine.contents > 0 && GetBlockTime(magazine.ammoType.shortname) > 0)
            {
                var item = player.inventory.AllItems().FirstOrDefault(x => x.GetHeldEntity() == weapon);
                if (item != null)
                {
                    item._condition = 0f;
                    item._maxCondition = 0f;
                    item.MarkDirty();
                    magazine.contents = 0;
                    magazine.capacity = 0; 
                }
            }
        }

        private void ShowUI(BasePlayer player, string time)
        {
            var container = new CuiElementContainer
            {
                new CuiElement
                {
                    Name = elem,
                    Parent = "Hud.Menu",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMessage("Item Blocked", player.UserIDString, time),
                            Align = TextAnchor.MiddleCenter,
                            FontSize = 20
                        },
                        new CuiRectTransformComponent {AnchorMin = "0.4 0.8", AnchorMax = "0.6 0.9"}
                    }
                }
            };

            CuiHelper.DestroyUi(player, elem);
            CuiHelper.AddUi(player, container);
            
            timer.Once(config.showTime, () =>
            {
                if (player != null)
                {
                    CuiHelper.DestroyUi(player, elem);
                }
            });
        }

        #endregion
        
        #region Time Support

        private double Now()
        {
            return DateTime.UtcNow.Subtract(new DateTime(2019, 1, 1, 0, 0, 0)).TotalSeconds;
        }

        private int Passed(double since)
        {
            return Convert.ToInt32(Now() - since);
        }

        private double SaveTime()
        {
            return SaveRestore.SaveCreatedTime.Subtract(new DateTime(2019, 1, 1, 0, 0, 0)).TotalSeconds;
        }

        private int PassedSinceWipe()
        {
            return Convert.ToInt32(Now() - SaveTime());
        }
        
        private string GetTimeString(int time)
        {
            var timeString = string.Empty;
            var days = time / 86400;
            time = time % 86400;
            if (days > 0)
            {
                timeString += days + "d";
            }
            
            var hours = time / 3600;
            time = time % 3600;
            if (hours > 0)
            {
                if (days > 0)
                {
                    timeString += ", ";
                }
                
                timeString += hours + "h";
            }
            
            var minutes = time / 60;
            time = time % 60;
            if (minutes > 0)
            {
                if (hours > 0)
                {
                    timeString += ", ";
                }
                
                timeString += minutes + "m";
            }

            var seconds = time;
            if (seconds > 0)
            {
                if (minutes > 0)
                {
                    timeString += ", ";
                }
                
                timeString += seconds + "s";
            }

            return timeString;
        }

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Item Blocked", "That item will be wipe-blocked still <color=red>{0}</color>!"}
            }, this);
        }

        private void Message(BasePlayer player, string messageKey, params object[] args)
        {
            if (player == null)
            {
                return;
            }

            var message = GetMessage(messageKey, player.UserIDString, args);
            player.ChatMessage(message);
        }

        private string GetMessage(string messageKey, string playerID, params object[] args)
        {
            return string.Format(lang.GetMessage(messageKey, this, playerID), args);
        }

        #endregion

        #region Configuration | 24.05.2020

        private static ConfigData config = new ConfigData();

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Shortname -> Block time")]
            public Dictionary<string, int> items = new Dictionary<string, int>();

            [JsonProperty(PropertyName = "Announcement duration")]
            public float showTime = 5f;
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
                for (var i = 0; i < 3; i++)
                {
                    PrintError("Configuration file is corrupt! Check your config file at https://jsonlint.com/");
                }
                
                LoadDefaultConfig();
                return;
            }

            ValidateConfig();
            SaveConfig();
        }

        private void ValidateConfig()
        {
            if (ConVar.Server.hostname.Contains("[DEBUG]") == true)
            {
                PrintWarning("Using default configuration on debug server");
                config = new ConfigData();
            }

            if (config.items.Count == 0)
            {
                config.items = new Dictionary<string, int>
                {
                    {"pistol.revolver", 86400},
                    {"pistol.python", 86400},
                    {"shotgun.pump", 86400},
                    {"smg.mp5", 86400},
                    {"pistol.m92", 86400},
                    {"rifle.m39", 86400},
                    {"lmg.m249", 86400},
                    {"rifle.lr300", 86400},
                    {"rifle.l96", 86400},
                    {"pistol.semiauto", 86400},
                    {"rifle.semiauto", 86400},
                    {"shotgun.spas12", 86400},
                    {"smg.thompson", 86400},
                    {"shotgun.waterpipe", 86400},
                    {"pistol.eoka", 86400},
                    {"rifle.ak", 86400},
                    {"rifle.bolt", 86400},
                    {"smg.2", 86400},
                    {"shotgun.double", 86400},

                    {"coffeecan.helmet", 86400},
                    {"heavy.plate.helmet", 86400},
                    {"heavy.plate.jacket", 86400},
                    {"heavy.plate.pants", 86400},
                    {"metal.plate.torso", 86400},
                    {"metal.facemask", 86400},
                    {"roadsign.kilt", 86400},
                    {"roadsign.jacket", 86400},
                    {"roadsign.gloves", 86400},

                    {"grenade.beancan", 86400},
                    {"flamethrower", 86400},
                    {"rocket.launcher", 86400},
                    {"multiplegrenadelauncher", 86400},
                    {"explosive.satchel", 86400},
                    {"explosive.timed", 86400},
                    {"surveycharge", 86400},
                    {"ammo.grenadelauncher.buckshot", 86400},
                    {"ammo.grenadelauncher.he", 86400},
                    {"ammo.grenadelauncher.smoke", 86400},
                    {"ammo.rifle.explosive", 86400},
                    {"ammo.rocket.basic", 86400},
                    {"ammo.rocket.fire", 86400},
                    {"ammo.rocket.hv", 86400},
                };
            }
        }

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion

        #region API

        private Dictionary<string, int> API_GetTimesLeft()
        {
            var value = new Dictionary<string, int>();

            foreach (var entry in config.items)
            {
                var blockLeft = entry.Value - PassedSinceWipe();

                if (blockLeft < 0)
                {
                    blockLeft = 0;
                }
                
                value.Add(entry.Key, blockLeft);
            }
            
            return value;
        }

        #endregion
    }
}