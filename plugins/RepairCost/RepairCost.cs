using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ConVar;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using UnityEngine;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("Repair Cost", "Rustoholics", "0.1.1")]
    [Description("Alter the repair cost of items")]

    public class RepairCost : CovalencePlugin
    {
        #region Dependencies
        
        [PluginReference] 
        private Plugin Economics;
        
        #endregion

        #region Variables
        
        private Dictionary<string,RepairBench> _repairBenches = new Dictionary<string,RepairBench>();
        private Dictionary<string,string> _repairGui = new Dictionary<string,string>();
        private Dictionary<string,ulong> _repairGuiItem = new Dictionary<string,ulong>();
        private Dictionary<string, Timer> _repairTimers = new Dictionary<string, Timer>();
        
        #endregion

        #region Config
        private Configuration _config;
        protected override void SaveConfig() => Config.WriteObject(_config);
        protected override void LoadDefaultConfig() => _config = new Configuration();

        private class Configuration
        {
            
            public Dictionary<string, Dictionary<string, object>> RepairCosts = new Dictionary<string, Dictionary<string, object>>
            {
                {"global", new Dictionary<string, object>
                    {
                        {"enabled", false},
                        {"multiplier", 5.0f},
                        {"items", new Dictionary<string, float>()},
                        {"economics", 0.0d}
                    }
                },
                {"smg.mp5", new Dictionary<string, object>
                    {
                        {"enabled", true},
                        {"multiplier", 6.0f},
                        {
                            "items", new Dictionary<string, float>
                            {
                                {"smgbody", 1.0f}
                            }
                        },
                        {"economics", 100.0d}
                    }
                },
                {"rifle.lr300", new Dictionary<string, object>
                    {
                        {"enabled", true},
                        {"multiplier", 10.0f},
                        {"items", new Dictionary<string, float>()},
                        {"economics", 100.0d}
                    }
                },
                {"pistol.m92", new Dictionary<string, object>
                    {
                        {"enabled", true},
                        {"multiplier", 5.0f},
                        {"items", new Dictionary<string, float>()},
                        {"economics", 100.0d}
                    }
                },
                {"rifle.l96", new Dictionary<string, object>
                    {
                        {"enabled", true},
                        {"multiplier", 10.0f},
                        {"items", new Dictionary<string, float>()},
                        {"economics", 100.0d}
                    }
                },
                {"lmg.m249", new Dictionary<string, object>
                    {
                        {"enabled", true},
                        {"multiplier", 10.0f},
                        {"items", new Dictionary<string, float>()},
                        {"economics", 100.0d}
                    }
                }
            };
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
                ["NotEnoughMaterials"] = "You do not have enough materials to repair this item",
                ["EconomicsRequired"] = " and {0} coins",
                ["NotEnoughCoins"] = "You do not have enough coins to make this repair"
            }, this);
        }
        
        #endregion
        
        #region Hooks
        void CanLootEntity(BasePlayer player, RepairBench container)
        {
            _repairBenches[player.UserIDString] = container;
        }
        
        void OnLootEntityEnd(BasePlayer player, RepairBench container)
        {
            if (_repairTimers.ContainsKey(player.UserIDString))
            {
                _repairTimers[player.UserIDString].Destroy();
                _repairTimers.Remove(player.UserIDString);
            }

            CloseGui(player);
            if (_repairBenches.ContainsKey(player.UserIDString))
            {
                _repairBenches.Remove(player.UserIDString);
            }
        }
        void OnLootEntity(BasePlayer player, RepairBench container)
        {
            CheckAndPresentGui(player, container);
        }
        
        object OnItemRepair(BasePlayer player, Item itemToRepair)
        {
            if (!IsCustomRepairItem(itemToRepair))
            {
                return null;
            }
            
            RepairBench repairBenchEntity = _repairBenches[player.UserIDString];

            var list = RepairList(player, itemToRepair);
            
            var economicsRequired = EconomicsRequired(itemToRepair);

            if (!PlayerHasEnough(player, list, economicsRequired))
            {
                player.ChatMessage(Lang("NotEnoughMaterials", player.UserIDString));
                return true;
            }
            
            if (Economics != null && economicsRequired > 0)
            {
                if (!Economics.Call<bool>("Withdraw", player.UserIDString, economicsRequired))
                {
                    player.ChatMessage(Lang("NotEnoughCoins", player.UserIDString));
                    return true;             
                }
            }
            
            foreach (ItemAmount itemAmount in list)
            {
                player.inventory.Take((List<Item>) null, itemAmount.itemid, (int)itemAmount.amount);
            }
            

            Facepunch.Pool.FreeList<ItemAmount>(ref list);
            itemToRepair.DoRepair(0.2f);
            
            if (Global.developer > 0)
                Debug.Log((object) ("Item repaired! condition : " + (object) itemToRepair.condition + "/" + (object) itemToRepair.maxCondition));
            Effect.server.Run("assets/bundled/prefabs/fx/repairbench/itemrepair.prefab", repairBenchEntity, 0U, Vector3.zero, Vector3.zero);
            return true;
        }
        
        // Remove any open GUIs on unloading the plugin, to avoid stuck GUIs
        void Unload()
        {
            foreach (var userIdString in _repairGui.Keys.ToArray())
            {
                var player = BasePlayer.FindByID(Convert.ToUInt64(userIdString));
                if (player != null && player.IsConnected)
                {
                    CloseGui(player);
                }
            }
        }
        
        void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            NextTick(() =>
            {
                if (item == null || container == null)
                {
                    return;
                }

                BasePlayer player;

                if (container.entityOwner != null && container.entityOwner is RepairBench)
                {
                    if (item.GetOwnerPlayer() == null) return;
                    
                    // Check if bench is empty
                    if (container.itemList.Count == 0)
                    {
                        player = item.GetOwnerPlayer();
                        CloseGui(player);
                    }

                    return;
                }

                if (item.GetRootContainer() == null) return;
                
                if (!(item.GetRootContainer().entityOwner is RepairBench)) return;
                
                if (container.GetOwnerPlayer() == null) return;
                
                player = container.GetOwnerPlayer();
                CheckAndPresentGui(player, (RepairBench)item.GetRootContainer().entityOwner);
            });
        }
        
        #endregion
        
        #region GUI

        void CheckAndPresentGui(BasePlayer player, RepairBench container)
        {
            var item = container.inventory.GetSlot(0);
            if (item != null && UseCustomRepairGui(item, player))
            {
                // Check if Repair GUI is already open
                if (!_repairGui.ContainsKey(player.UserIDString))
                {
                    WriteGui(player, item); // It's not open so write it
                }else if(_repairGuiItem[player.UserIDString] != item.uid)
                {
                    CloseGui(player); // It's open but for a different item, so close that GUI and open a new one
                    WriteGui(player, item);
                }
            }
            else
            {
                CloseGui(player);
            }
        }

        bool UseCustomRepairGui(Item item, BasePlayer player)
        {
            if (!IsCustomRepairItem(item))
            {
                return false;
            }
            ItemDefinition targetItem = (UnityEngine.Object) item.info.isRedirectOf != (UnityEngine.Object) null ? item.info.isRedirectOf : item.info;
            if ((player.blueprints.HasUnlocked(targetItem) ? 1 : (!((UnityEngine.Object) targetItem.Blueprint != (UnityEngine.Object) null) ? 0 : (!targetItem.Blueprint.isResearchable ? 1 : 0))) == 0)
                return false;
            
            if (!item.info.condition.repairable || item.condition.Equals(item.maxCondition))
            {
                return false;
            }

            return true;
        }
        
        void WriteGui(BasePlayer player, Item itemToRepair)
        {
            var list = RepairList(player, itemToRepair);
            var economicsRequired = EconomicsRequired(itemToRepair);
            if (list == null)
            {
                return;
            }
            var panel = new CuiPanel();
            if (itemToRepair.info.HasSkins)
            {
                panel.RectTransform.AnchorMin = "0.5 0";
                panel.RectTransform.AnchorMax = "0.5 0";
                panel.RectTransform.OffsetMin = "326 343";
                panel.RectTransform.OffsetMax = "564 413";
            }
            else
            {
                panel.RectTransform.AnchorMin = "0.5 0";
                panel.RectTransform.AnchorMax = "0.5 0";
                panel.RectTransform.OffsetMin = "326 179";
                panel.RectTransform.OffsetMax = "564 249";
            }

            panel.Image.Color = "0.25 0.24 0.22 1.0";

            var text = new CuiLabel();
            text.Text.Color = "1.0 1.0 1.0 1.0";
            if (!PlayerHasEnough(player, list, economicsRequired))
            {
                text.Text.Color = "0.73 0.4 0.29 1.0";
            }
            text.Text.Align = TextAnchor.MiddleCenter;
            text.Text.FontSize = 12;
            text.Text.Font = "RobotoCondensed-Regular.ttf";
            text.Text.Text = "Repair cost: ";

            var n = 0;
            foreach (var i in list)
            {
                if (n > 0)
                {
                    text.Text.Text += ", ";
                }

                text.Text.Text += Convert.ToString(i.amount) + " " + i.itemDef.displayName.english;
                n++;
            }

            if (economicsRequired > 0)
            {
                text.Text.Text += Lang("EconomicsRequired", player.UserIDString, economicsRequired);
            }

            var container = new CuiElementContainer();
            var menu = container.Add(panel, "Overlay");
            
            if (_repairGui.ContainsKey(player.UserIDString))
            {
                return; // Final check to make sure this player doesn't have a GUI already
            }
            container.Add(text, menu, "RepairText");
            _repairGui[player.UserIDString] = menu;
            _repairGuiItem[player.UserIDString] = itemToRepair.uid;
            
            CuiHelper.AddUi(player, container);
        }

        void CloseGui(BasePlayer player)
        {
            if (!_repairGui.ContainsKey(player.UserIDString))
            {
                return;
            }
            CuiHelper.DestroyUi(player, _repairGui[player.UserIDString]);
            _repairGui.Remove(player.UserIDString);
            _repairGuiItem.Remove(player.UserIDString);
        }
        
        #endregion
        
        #region Repair Items List and Amount Modifiers
        private List<ItemAmount> RepairList(BasePlayer player, Item itemToRepair)
        {
            ItemDefinition info = itemToRepair.info;
            ItemBlueprint component = info.GetComponent<ItemBlueprint>();
            
            List<ItemAmount> list = Facepunch.Pool.GetList<ItemAmount>();
            RepairBench.GetRepairCostList(component, list);

            return ApplyItemListMultiplier(list, itemToRepair);
        }

        private List<ItemAmount> ApplyItemListMultiplier(List<ItemAmount> list, Item itemToRepair)
        {
            List<ItemAmount> newlist = new List<ItemAmount>();
            var repairCostFraction = RepairBench.RepairCostFraction(itemToRepair);
            float multiplier = 1;

            if (_config.RepairCosts.ContainsKey(itemToRepair.info.shortname) && _config.RepairCosts[itemToRepair.info.shortname].ContainsKey("multiplier"))
            {
                multiplier = Convert.ToSingle(_config.RepairCosts[itemToRepair.info.shortname]["multiplier"]);
            }
            else if(_config.RepairCosts.ContainsKey("global") && _config.RepairCosts["global"].ContainsKey("enabled") && (bool)_config.RepairCosts["global"]["enabled"] && _config.RepairCosts["global"].ContainsKey("multiplier"))
            {
                multiplier = Convert.ToSingle(_config.RepairCosts["global"]["multiplier"]);
            }

            foreach (ItemAmount itemAmount in list)
            {
                if (itemAmount.itemDef.category != ItemCategory.Component)
                {
                    itemAmount.amount = Mathf.CeilToInt(itemAmount.amount * repairCostFraction * multiplier);
                    newlist.Add(itemAmount);
                }
            }

            // Add any extra items
            if (_config.RepairCosts[itemToRepair.info.shortname].ContainsKey("items"))
            {
                var items = (IEnumerable)_config.RepairCosts[itemToRepair.info.shortname]["items"];
                var json = JsonConvert.SerializeObject(items);
                var dictionary = JsonConvert.DeserializeObject<Dictionary<string, float>>(json);
                foreach (var customItem in dictionary)
                {
                    var itemDefinition = ItemManager.FindItemDefinition(customItem.Key);
                    if (itemDefinition == null) // Could not find an item definition matching this key, skip it
                    {
                        continue;
                    }

                    var isNew = true;
                    foreach (var newItem in newlist)
                    {
                        // The item is already in the list, so override it
                        if (newItem.itemid == itemDefinition.itemid)
                        {
                            newItem.amount =  Mathf.CeilToInt(Convert.ToSingle(customItem.Value));
                            isNew = false;
                            break;
                        }
                    }

                    if (isNew)
                    {
                        newlist.Add(new ItemAmount(itemDefinition, customItem.Value));
                    }
                }
            }
            
            Facepunch.Pool.FreeList<ItemAmount>(ref list);
            
            return newlist;
        }
        
        #endregion

        #region Checking Functions
        bool IsCustomRepairItem(Item item)
        {
            if (_config.RepairCosts.ContainsKey(item.info.shortname) && _config.RepairCosts[item.info.shortname].ContainsKey("enabled"))
            {
                return (bool)_config.RepairCosts[item.info.shortname]["enabled"];
            }
            if (_config.RepairCosts.ContainsKey("global") && _config.RepairCosts["global"].ContainsKey("enabled"))
            {
                return (bool)_config.RepairCosts["global"]["enabled"];
            }

            return false;
        }

        bool PlayerHasEnough(BasePlayer player, List<ItemAmount> list, double economics=0d)
        {
            foreach (ItemAmount itemAmount in list)
            {
            
                int amount = player.inventory.GetAmount(itemAmount.itemDef.itemid);
                if (itemAmount.amount > amount)
                {
                    return false;
                }
            }

            if (Economics != null && economics > 0)
            {
                if (Economics.Call<double>("Balance", player.UserIDString) < economics)
                {
                    return false;
                }
            }

            return true;
        }

        double EconomicsRequired(Item item)
        {
            if (Economics == null)
            {
                return 0d;
            }
            if (_config.RepairCosts.ContainsKey(item.info.shortname) &&
                _config.RepairCosts[item.info.shortname].ContainsKey("enabled") && 
                _config.RepairCosts[item.info.shortname].ContainsKey("economics") &&
                Convert.ToDouble(_config.RepairCosts[item.info.shortname]["economics"]) > 0)
            {
                return Convert.ToDouble(_config.RepairCosts[item.info.shortname]["economics"]);
            }
            if (_config.RepairCosts.ContainsKey("global") &&
                      _config.RepairCosts["global"].ContainsKey("economics") &&
                      Convert.ToDouble(_config.RepairCosts["global"]["economics"]) > 0)
            {
                return Convert.ToDouble(_config.RepairCosts["global"]["economics"]);
            }
            return 0d;
        }
        
        #endregion
        
    }
}