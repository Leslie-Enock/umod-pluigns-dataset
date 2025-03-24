using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using WebSocketSharp;
using UnityEngine;
using System.Linq;
using System.Globalization;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using Oxide.Plugins.EasyResearchEx;
using Rust;

/*
 *  Copyright 2022 khan
Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

http://www.apache.org/licenses/LICENSE-2.0
 
Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License. 
 */

/*
* This is a re-write version 2.0.0
* Delete the old Language File and Config. (if updating from version 1x )
* Added Option to only notify that you have already researched the item.
* Added Economics Support
* Added ServerRewards Support
* Added Custom Currency options
* Updated PopupNotifications Support
* Added Sound Effect Options.
* Added Custom Currency Rust Default settings.
* Added ChatPrefix

* This update 2.0.1
Delete old lang file
* Added Research Requirement option
* Updated lang
* Added Research Duration options
* Updated Bypass permission 

* This update 2.0.2
* Added ability to set permissions to each item.
Warning will most likely over-ride the old config.
when it adds the new value. 

* This update 2.0.3
* Added Tech Tree support ( All that I can support )
* Added Block Tech Tree Researching toggle ( Default is false )
if that is false it will check if a blocked item 
is in the blocked list and block that from being unlocked as well.
if you have the bypass perm it will let you unlock the tech tree
stuff as well just like research table. 

* This update 2.0.4
* Added full Permission support to the tech tree.
* Updated Lang responses so it only shows the permission name 
and not the whole easyresearch. half 

( 2.1.0 Beta )
Beta Release for newly requested features. Stable Version 2.1.0
Added More Config Options 
Improved performance. 
Added First Time Research Cost ( if unlocking in TechTree )
Added Separate Earned amount from researching amount costs ( Research Table ) Added UI Systems for both TechTree and Research table.
( Tech Tree UI is still under work for better implementation but works for now )
Will auto take researched blueprint only if you have already learned it.
If you have not yet learned it and want to research it cheaper at the research table it will remain in there for you to take/learn.

( Beta 2.1.1 )
Beta Update 2.1.1
Fixed unlock path problem
Fixed a few UI Issues/bugs
Beta Release for newly requested features. Stable Version 2.1.0
Added More Config Options 
Improved performance. 
Added First Time Research Cost ( if unlocking in TechTree )
Added Separate Earned amount from researching amount costs ( Research Table ) Added UI Systems for both TechTree and Research table.
( Tech Tree UI is still under work for better implementation but works for now )
Will auto take researched blueprint only if you have already learned it.
If you have not yet learned it and want to research it cheaper at the research table it will remain in there for you to take/learn.

 Beta update 2.1.2
 Fixes for rust update + added support for XPerience plugin
 
 Beta Update 2.1.1
Fixed unlock path problem
Fixed a few UI Issues/bugs
Beta Release for newly requested features. Stable Version 2.1.0
Added More Config Options 
Improved performance. 

Added First Time Research Cost ( if unlocking in TechTree )
Added Separate Earned amount from researching amount costs ( Research Table ) Added UI Systems for both TechTree and Research table.
( Tech Tree UI is still under work for better implementation but works for now )

Will auto take researched blueprint only if you have already learned it.
If you have not yet learned it and want to research it cheaper at the research table it will remain in there for you to take/learn.

 ( Release stable 2.1.0 )
 * Major Update 2.1.0
 * Delete Lang File!
 * Fixes for rust update
 * Added Support for Notify By Mevent
 * Added Full Support to work with XPerience features!
 * Added Support for LangAPI
 * Fixed unlock path problem
 * Fixed a few UI Issues/bugs
 * Added More Config Options
 * Improved performance.
 * Added First Time Research Cost ( if unlocking in TechTree )
 * Added Separate Earned amount from researching amount costs ( Research Table ) 
 * Added UI Systems for both TechTree and Research table.
 * Added Random Success Chances for each item being researched ( Research Table )

 * Will auto take researched blueprint only if you have already learned it.
 * If you have not yet learned it and want to research it cheaper at the research table it will remain in there for you to take/learn.

( Tech Tree UI is still under work for better implementation but works for now )

* Update 2.1.1
 * Fixed plugin not working when xperience is not loaded.

 * Update 2.1.2
 * Fixed XPerience support
 * Fixed Plugin not working when XPerience was unloaded.
 * Fixed Tech Tree Not working properly
 * Fixed Research Table showing wrong values
 * Fixed Popup notification setting
 * Fixed Notify notification settings
 * Fixed Custom UI displays
 * Rewrote and added new hooks for XPerience plugin version 1.1.6+ update!
   Shout out to wajeeh93 for all the testing support!

 * Update 2.1.3
 * Added missing arguments to fix compilation error in rusts game update.
 * Ported changes to sync builds with Advanced Researching build.
 * Code Cleanup
 */

// TODO Update Message Systems between UI/PopUpResponses + Finish Custom UI Creation + Create UI Shop + Create UI Editor

namespace Oxide.Plugins
{
    [Info("Easy Research", "Khan", "2.1.3")]
    [Description("Adds new features to TechTree, Research Table & Research Systems")]

    public class EasyResearch : CovalencePlugin
    {
        #region License

        /*
         *  Copyright 2022 khan
        Licensed under the Apache License, Version 2.0 (the "License");
        you may not use this file except in compliance with the License.
        You may obtain a copy of the License at

        http://www.apache.org/licenses/LICENSE-2.0
         
        Unless required by applicable law or agreed to in writing, software
        distributed under the License is distributed on an "AS IS" BASIS,
        WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
        See the License for the specific language governing permissions and
        limitations under the License. 
         */

        #endregion

        #region Plugin Reference

        [PluginReference]
        private Plugin PopupNotifications, Economics, ServerRewards, XPerience, Notify, LangAPI;

        #endregion

        #region Fields

        PluginConfig _confg;
        private const string UsePerm = "easyresearch.use";
        private const string ByPass = "easyresearch.bypass";

        #endregion

        #region Config

        private void CheckConfig()
        {
            foreach (ItemBlueprint item in ItemManager.GetBlueprints())
            {
                if (!item.isResearchable || item.defaultBlueprint) continue;
                if (_confg.ResearchSettings.ContainsKey(item.targetItem.shortname)) continue;
                _confg.ResearchSettings.Add(item.targetItem.shortname, new Research
                {
                    DisplayName = item.targetItem.displayName.english,
                    EarnCurrencyAmount = ResearchTable.ScrapForResearch(item.targetItem, ResearchTable.ResearchType.ResearchTable),
                    TechTreeFirstUnlockCost = ResearchTable.ScrapForResearch(item.targetItem, ResearchTable.ResearchType.TechTree),
                    CostToResearchAmount = ResearchTable.ScrapForResearch(item.targetItem, ResearchTable.ResearchType.ResearchTable),
                    ResearchDuration = item.time,
                    ResearchSuccessChance = 100
                });
            }

            SaveConfig();
        }

        private class PluginConfig
        {
            [JsonProperty("Chat Prefix")] 
            public string ChatPrefix = "<color=#32CD32>Easy Research</color>: ";
            
            [JsonProperty("Research Requirement this is the type of resource used to research items (Expects Item Shortname)")]
            public string Requirement = "scrap";
            
            [JsonProperty("Already Researched Toggle")]
            public bool Researched = false;
            
            [JsonProperty("Block Tech Tree Researching")]
            public bool BlockTechTree = false;

            [JsonProperty("Use Popup Notifications")]
            public bool Popup = false;
            
            [JsonProperty("Use Notify Notifications (Mevents Version CodeFling)")]
            public bool Notify = false;
            
            [JsonProperty("Notify Notification Type")]
            public int NotifyType = 0;

            [JsonProperty("Use CustomUI Overlay Notifications")]
            public bool CustomUI = true;

            [JsonProperty("Enable Economics")] 
            public bool Economics = true;

            [JsonProperty("Enable ServerRewards")] 
            public bool ServerRewards = false;

            [JsonProperty("Enable Custom Currency")]
            public bool Custom = false;

            [JsonProperty("Custom Name")] 
            public string CName = "";

            [JsonProperty("Custom Item ID")] 
            public int CId = 0;

            [JsonProperty("Custom SkinId")] 
            public ulong CSkinId = 0;
            
            [JsonProperty("Research Table UI Options")]
            public ResearchTableUI ResearchTableUI = new ResearchTableUI();
            
            [JsonProperty("TechTree UI Options")]
            public TechTreeUI TechTreeUI = new TechTreeUI();
            
            [JsonProperty("Blocked Items")] 
            public List<string> Blocked = new List<string>();

            [JsonProperty("Custom Research Options")]
            public Dictionary<string, Research> ResearchSettings = new Dictionary<string, Research>();

            public string ToJson() => JsonConvert.SerializeObject(this);
            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        public class Research
        {
            public string DisplayName;
            public double EarnCurrencyAmount;
            public int TechTreeFirstUnlockCost;
            public int CostToResearchAmount;
            public float ResearchDuration;
            public float ResearchSuccessChance = 100;
            public string SetPermission = "";
            [JsonIgnore]
            public string PrefixPermission => "easyresearch." + SetPermission;
        }

        public class ResearchTableUI
        {
            public string CostColor = "#FFFFFF";
            public string CurrencyColor = "#ff3333";
        }

        public class TechTreeUI
        {
            public string CostColor = "#ff3333";
        }

        #endregion

        #region LangSystem
        private string EasyResearchLang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["AlreadyUnlocked"] = "You already unlocked {0}",
                ["AddedCurrency"] = "has deposited {0} coins into your account",
                ["Blocked"] = "{0} is not researchable",
                ["Requires"] = "New requirement for researching is now {0}",
                ["NoPerm"] = "You do not have permission {0} to research {1}",
                ["AmountTo"] = "{0} needs {1} {2} to research it",
                ["TechTreeUI"] = "{0} requires {1} {2} to research",
                ["ResearchedRolled"] = "Researching {0} Failed!",
                ["InvalidShortname"] = "Not a valid item shortname {0} set for research requirement! \n List of valid item shortnames can be found at https://www.corrosionhour.com/rust-item-list/",
                ["NowResearching"] = "Now Researching {0} for {1} {2}",
            }, this);
        }
        private void PopupMessage(IPlayer player, string message)
        {
            if (_confg.CustomUI)
            {
                player.Reply(_confg.ChatPrefix + message);
                return;
            }
            if (_confg.Popup && PopupNotifications != null)
            {
                PopupNotifications?.Call("CreatePopupNotification", _confg.ChatPrefix + message);
                return;
            }
            if (_confg.Notify && Notify.IsLoaded)
            {
                string msg = string.Format(_confg.ChatPrefix + message);
                Notify?.Call("SendNotify", player.Object as BasePlayer, _confg.NotifyType, msg);
                return;
            }
            player.Reply(_confg.ChatPrefix + message);
        }

        #endregion

        #region Oxide

        protected override void LoadDefaultConfig() => _confg = new PluginConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _confg = Config.ReadObject<PluginConfig>();
                if (_confg == null)
                {
                    throw new JsonException();
                }
                if (!_confg.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    PrintWarning("EasyResearch Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                PrintWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_confg, true);

        private void Loaded()
        {
            foreach (var item in _confg.ResearchSettings.Values)
            {
                if (item.SetPermission.IsNullOrEmpty()) continue;
                
                if (permission.PermissionExists(item.PrefixPermission, this)) continue;

                permission.RegisterPermission(item.PrefixPermission, this);
            }
        }

        private void OnServerInitialized()
        {
            permission.RegisterPermission(UsePerm, this);
            permission.RegisterPermission(ByPass, this);
            CheckConfig();
            UpdateResearchTables(_confg.Requirement);
        }

        private void Unload()
        {
            UpdateResearchTables();
            foreach (IPlayer player in players.Connected)
            {
                if (player != null && player.Id.IsSteamId())
                    DestroyUI(player.Object as BasePlayer);
            }
        }

        #endregion

        #region CoreSystem

        // Setting Custom Research Cost Item on newly spawned tables
        private void OnEntitySpawned(ResearchTable table)
        {
            if (permission.UserHasPermission(table.OwnerID.ToString(), UsePerm))
                table.researchResource = ItemManager.FindItemDefinition(_confg.Requirement);
        }

        // Need to make sure the ui system is clear before creating any on the Research Table
        private void OnLootEntity(BasePlayer player, ResearchTable table)
        {
            if (player == null || table == null || !player.UserIDString.IsSteamId()) return;
            if (table is ResearchTable)
            {
                DestroyUI(player);
                if (table.inventory.IsEmpty() || table.GetTargetItem() == null || table.GetTargetItem()?.amount == 0) return;
               var item = table?.inventory?.GetSlot(0);
               if (table.GetTargetItem().info.shortname == _confg.Requirement || item.isBroken || item.IsBlueprint()) return;
               if (item.amount <= 1)
                {
                    Research research = FindResearch(table.GetTargetItem().info.shortname);
                    int amount = 0;
                    if (research != null)
                    {
                        if (XPerience != null && XPerience.IsLoaded)
                            amount = XPerience.Call<int>("OnResearchCost", research.CostToResearchAmount, player);
                        else
                            amount = research.CostToResearchAmount;
                    }
                    string msg = amount > 0 ? amount.ToString() : "N/A";
                    CuiElementContainer CostContainer = new CuiElementContainer();
                    PanelUI1(ref CostContainer, "Overlay", "RCost", "0.5 0", "0.5 0", "445 292", "572 372",  Color.ToRGB("#000000"));
                    LableUI2(ref CostContainer, "RCost", "Added", Color.ToRGB(_confg.ResearchTableUI.CostColor, 1f), msg, 50, "0 0", "1 1");
                    CuiHelper.AddUi(player, CostContainer);
                }
            }
        }

        // Make sure to clean up UI System if they are done with the ResearchTable
        private void OnLootEntityEnd(BasePlayer player, ResearchTable entity)
        {
            if (player == null || entity == null || !player.UserIDString.IsSteamId()) return;
            if (entity is ResearchTable)
                DestroyUI(player);
        }

        // Checking for other benches and cleaning up before opening ui
        private void OnEntityEnter(TriggerWorkbench trigger, BasePlayer player)
        {
            if (player == null || player.IsNpc || !player.userID.IsSteamId()) return;
            DestroyUI(player);
        }

        // When they walk away from benches clean up
        private void OnEntityLeave(TriggerWorkbench trigger, BasePlayer player)
        {
            if (player == null || player.IsNpc || !player.userID.IsSteamId()) return;
            DestroyUI(player);
        }

        private Dictionary<Rarity, int> _rarity = new Dictionary<Rarity, int>
        {
            {Rarity.None, 500},
            {Rarity.Common, 20},
            {Rarity.Uncommon, 75},
            {Rarity.Rare, 125},
            {Rarity.VeryRare, 500},
        };

        private object OnResearchCostDetermine(Item item, ResearchTable researchTable)
        {
            Research research = FindResearch(item.info.shortname);
            researchTable.researchResource = ItemManager.FindItemDefinition(_confg.Requirement);

            int amount = research?.CostToResearchAmount ?? _rarity[item.info.rarity];

            if (XPerience != null && XPerience.IsLoaded)
                amount = XPerience.Call<int>("OnResearchCost", amount, researchTable.user);

            return amount;
        }

        // Update UI + Null check everything...
        private void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (container?.entityOwner == null || container?.entityOwner is ResearchTable == false || item == null) return;
            if (container.entityOwner is ResearchTable && item.info.Blueprint != null && container.GetSlot(0) != null && container.GetSlot(0).amount > 0 && container.GetSlot(0).info.Blueprint != null && !item.IsBlueprint() && item.info.shortname != _confg.Requirement && !item.isBroken && item.info.Blueprint.isResearchable)
            {
                var table = container.entityOwner as ResearchTable;
                var player = table?.user;
                if (player != null)
                    DestroyUI(player);
                if (table == null || !table.GetTargetItem().info.Blueprint.isResearchable) return;
                Research research = FindResearch(container.GetSlot(0).info.shortname);
                int amount = 0;
                if (research != null)
                {
                    if (XPerience != null && XPerience.IsLoaded)
                        amount = XPerience.Call<int>("OnResearchCost", research.CostToResearchAmount, player);
                    else
                        amount = research.CostToResearchAmount;
                }
                string msg = amount > 0 ? amount.ToString() : "N/A";
                CuiElementContainer CostContainer = new CuiElementContainer();
                PanelUI1(ref CostContainer, "Overlay", "RCost", "0.5 0", "0.5 0", "445 292", "572 372",  Color.ToRGB("#000000"));
                LableUI2(ref CostContainer, "RCost", "Added", Color.ToRGB(_confg.ResearchTableUI.CostColor, 1f), msg, 50, "0 0", "1 1");
                CuiHelper.AddUi(player, CostContainer);

                /*int test = 0;
                if (table?.inventory?.GetSlot(1)?.amount > 0)
                {
                    test = table.GetScrapItem().amount;
                }
                if (test < amount || amount == 0)
                {
                    CuiElementContainer ButtonContainer = new CuiElementContainer();
                    PanelUI1(ref ButtonContainer, "Overlay", "Button", "0.5 0", "0.5 0", "436 116", "565 148", Color.ToRGB("#000000"));
                    LableUI2(ref ButtonContainer, "Button", "Stop", Color.ToRGB("#FFFFFF", 1f), "CANT RESEARCH", 16, "0 0", "1 1");
                    CuiHelper.AddUi(player, ButtonContainer);
                }*/
            }
        }

        // Update UI
        private void OnItemRemovedFromContainer(ItemContainer itemContainer, Item item, ResearchTable researchTable)
        {
            if (itemContainer?.entityOwner is ResearchTable)
            {
                researchTable = itemContainer?.entityOwner as ResearchTable;
            }
            if (researchTable == null && item?.parent?.entityOwner is ResearchTable)
            {
                researchTable = item?.parent?.entityOwner as ResearchTable;
            }
            if (researchTable == null) return;
            CuiHelper.DestroyUi(researchTable.user, "RCost");
            CuiHelper.DestroyUi(researchTable.user, "Button");
        }

        // Research Table Checks First 0 is item, 1 is scrap
        private object CanResearchItem(BasePlayer player, Item targetItem)
        {
            if (player == null || targetItem == null || !permission.UserHasPermission(player.UserIDString, UsePerm)) return null;
            DestroyUI(player);
            Research research = FindResearch(targetItem.info.shortname);
            if (research == null) return null;
            var itemdefinition = ItemManager.FindItemDefinition(_confg.Requirement);
            if (itemdefinition == null)
            {
                PrintError(EasyResearchLang("InvalidShortname", null, _confg.Requirement));
                return null;
            }

            string text = LangAPI?.Call<string>("GetItemDisplayName", targetItem.info.shortname, targetItem.info.displayName.english, player.UserIDString) ?? targetItem.info.displayName.english;
            string require = LangAPI?.Call<string>("GetItemDisplayName", itemdefinition.shortname, itemdefinition.displayName.english, player.UserIDString) ?? itemdefinition.displayName.english;

            if (_confg.Blocked.Contains(targetItem.info.shortname) && !permission.UserHasPermission(player.UserIDString, ByPass))
            {
                PopupMessage(player.GetPlayer(), EasyResearchLang("Blocked", player.UserIDString, text)); return false;
            }
            if (!research.SetPermission.IsNullOrEmpty() && !permission.UserHasPermission(player.UserIDString, research.PrefixPermission))
            {
                PopupMessage(player.GetPlayer(), EasyResearchLang("NoPerm", player.UserIDString, research.SetPermission, text)); return false;
            }
            if (_confg.Researched && player.blueprints.HasUnlocked(targetItem.info))
            {
                PopupMessage(player.GetPlayer(), EasyResearchLang("AlreadyUnlocked", player.UserIDString, text)); return false;
            }

            var item = targetItem?.GetRootContainer()?.GetSlot(1);

            var cost = 0;
            if (XPerience != null && XPerience.IsLoaded)
                cost = XPerience.Call<int>("OnResearchCost", research.CostToResearchAmount, player);
            else
                cost = research.CostToResearchAmount > 0 ? research.CostToResearchAmount : 0;

            if (cost == 0)
                return null;

            if (item?.info.shortname != _confg.Requirement || item?.amount < cost)
            {
                DestroyUI(player);
                CuiElementContainer menuContainer = new CuiElementContainer();
                PanelUI2(ref menuContainer, "Overlay", "Main", "0.77 0.798", "0.9465 0.835", Color.ToRGB("FFF5E1", 0.16f));
                LabelUI1(ref menuContainer, "Main", "Status", new Rectangle(0.02m, 0, 1m, 1), Color.ToRGB(_confg.ResearchTableUI.CostColor), EasyResearchLang("TechTreeUI", player.UserIDString, text, cost, require), 12);
                CuiHelper.AddUi(player, menuContainer);
                return false;
            }

            if (player.blueprints.HasUnlocked(targetItem.info) && cost > 0 && GiveCurrency(player, targetItem.info.shortname))
            {
                if (_confg.CustomUI)
                {
                    DestroyUI(player);
                    CuiElementContainer menuContainer = new CuiElementContainer();
                    PanelUI2(ref menuContainer, "Overlay", "Main", "0.77 0.798", "0.9465 0.835", Color.ToRGB("FFF5E1", 0.16f));
                    string currencymsg = String.Empty;
                    if (_confg.Economics)
                        currencymsg = "Coins";
                    if (_confg.ServerRewards)
                        currencymsg = "RP";
                    if (_confg.Custom)
                        currencymsg = ItemManager.FindItemDefinition(_confg.CId).displayName.english;
                    LabelUI1(ref menuContainer, "Main", "Status", new Rectangle(0.02m, 0, 1m, 1), Color.ToRGB(_confg.ResearchTableUI.CurrencyColor), EasyResearchLang("NowResearching", player.UserIDString, text, research.EarnCurrencyAmount, currencymsg), 12);
                    CuiHelper.AddUi(player, menuContainer);
                }
            }

            if (player.blueprints.HasUnlocked(targetItem.info))
            {
                ItemContainer container = targetItem.GetRootContainer();
                Item scrapItem = container.GetSlot(1);
                if (scrapItem.amount != cost)
                {
                    container.Remove(targetItem);
                    targetItem.Remove();
                    scrapItem.UseItem(cost);
                }
                else
                {
                    container.Remove(targetItem);
                    targetItem.Remove();
                    scrapItem.removeTime = research.ResearchDuration;
                }
            }

            return null;
        }

        // Setting Research Duration times
        private void OnItemResearch(ResearchTable table, Item targetItem, BasePlayer player)
        {
            if (player == null || !permission.UserHasPermission(player.UserIDString, UsePerm)) return;
            CuiHelper.DestroyUi(player, "Button");
            Research research = FindResearch(targetItem.info.shortname);
            if (research == null) return;

            float cost = research.ResearchDuration;
            if (XPerience != null && XPerience.IsLoaded)
                cost = XPerience.Call<float>("OnItemResearchReduction", research.ResearchDuration, player);

            table.researchDuration = cost;
        }

        private float OnItemResearched(ResearchTable table, float chance)
        {
            if (table.GetTargetItem() == null) return 1;
            var item = table.GetTargetItem();
            Research research = FindResearch(item.info.shortname);
            if (research == null) return 1;

            if ((int)research.ResearchSuccessChance != 100)
            {
                if (UnityEngine.Random.Range(0, 100) >= research.ResearchSuccessChance)
                    return 1;

                BasePlayer player = table.user;

                if (player == null)
                    player = item.GetOwnerPlayer();

                if (player != null)
                {
                    string text = LangAPI?.Call<string>("GetItemDisplayName", item.info.shortname, item.info.displayName.english, player.UserIDString) ?? item.info.displayName.english;
                    PopupMessage(player.IPlayer, EasyResearchLang("ResearchedRolled", player.UserIDString, text));
                }

            }
            
            return 0;
        }

        #endregion

        #region TechTree

        // hook broken/pointless
        /*private int? OnResearchCostDetermine(ItemDefinition itemDefinition, Research research)
        {
            _confg.ResearchSettings.TryGetValue(itemDefinition.shortname, out research);
            return research?.TechTreeFirstUnlockCost;
        }*/

        // Check Unlock Path
        private bool CheckUnlockPath(BasePlayer player, TechTreeData.NodeInstance node, TechTreeData techTree)
        {
            if (node.inputs.Count == 0) return true;
            var unlockPath = false;

            foreach (int nodeId in node.inputs)
            {
                var selectNode = techTree.GetByID(nodeId);
                if (selectNode.itemDef == null) return true;

                if (!techTree.HasPlayerUnlocked(player, selectNode)) continue;

                if (CheckUnlockPath(player, selectNode, techTree))
                    unlockPath = true;
            }
            
            return unlockPath;
        }

        // Checking Everything First
        private object CanUnlockTechTreeNode(BasePlayer player, TechTreeData.NodeInstance node, TechTreeData techTree)
        {
            if (player == null || !permission.UserHasPermission(player.UserIDString, UsePerm))
                return null;

            DestroyUI(player);

            if (!player.blueprints.HasUnlocked(node.itemDef) && _confg.BlockTechTree)
                return false;

            var itemdefinition = ItemManager.FindItemDefinition(_confg.Requirement);
            if (itemdefinition == null)
            {
                PrintError(EasyResearchLang("InvalidShortname", null, _confg.Requirement));
                return null;
            }
            string text = LangAPI?.Call<string>("GetItemDisplayName", node.itemDef.shortname, node.itemDef.displayName.english, player.UserIDString) ?? node.itemDef.displayName.english;
            string require = LangAPI?.Call<string>("GetItemDisplayName", itemdefinition.shortname, itemdefinition.displayName.english, player.UserIDString) ?? itemdefinition.displayName.english;

            if (_confg.Blocked.Contains(node.itemDef.shortname) && !permission.UserHasPermission(player.UserIDString, ByPass))
            {
                PopupMessage(player.IPlayer, EasyResearchLang("Blocked", player.UserIDString, text)); 
                return false;
            }

            Research research = FindResearch(node.itemDef.shortname);
            if (research == null)
                return null;

            if (!research.SetPermission.IsNullOrEmpty() && !permission.UserHasPermission(player.UserIDString, research.PrefixPermission))
            {
                PopupMessage(player.IPlayer, EasyResearchLang("NoPerm", player.UserIDString, research.SetPermission, text)); 
                return false;
            }

            CuiHelper.DestroyUi(player, "Tree");

            // Added A new Hook for Research related plugins to use specifically for the tech tree.
            // Example expects thee item amount and the player
            var cost = 0;
            if (XPerience != null && XPerience.IsLoaded)
                cost = XPerience.Call<int>("OnResearchCost", research.TechTreeFirstUnlockCost, player);
            else
                cost = research.TechTreeFirstUnlockCost > 0 ? research.TechTreeFirstUnlockCost : 0;

            if (cost == 0)
                return null;

            techTree.GetEntryNode().costOverride = cost;

            if (player.inventory.GetAmount(itemdefinition.itemid) < cost)
            {
                if (_confg.Notify || _confg.Popup)
                    PopupMessage(player.IPlayer, EasyResearchLang("TechTreeUI", player.UserIDString, text, cost, require));
                else
                {
                    CuiElementContainer menuContainer = new CuiElementContainer();
                    PanelUI2(ref menuContainer, "Overlay", "Tree", "0.807 0.198", "0.992 0.235", Color.ToRGB("FFF5E1", 0.16f));
                    LabelUI1(ref menuContainer, "Tree", "Status", new Rectangle(0.02m, 0, 1m, 1), Color.ToRGB(_confg.TechTreeUI.CostColor), EasyResearchLang("TechTreeUI", player.UserIDString, text, cost, require), 12);
                    CuiHelper.AddUi(player, menuContainer);
                }
                return false;
            }

            if (!CheckUnlockPath(player, node, techTree))
                return false;

            CuiHelper.DestroyUi(player, "Tree");
            return true;
        }

        // Final Checks Finished Taking item cost for techtree first unlock.
        private object OnTechTreeNodeUnlock(Workbench workbench, TechTreeData.NodeInstance node, BasePlayer player)
        {
            Research research = FindResearch(node.itemDef.shortname);
            if (research == null || player == null || !permission.UserHasPermission(player.UserIDString, UsePerm)) return null;
            int itemid = ItemManager.FindItemDefinition(_confg.Requirement).itemid;

            var cost = 0;
            if (XPerience != null && XPerience.IsLoaded)
                cost = XPerience.Call<int>("OnResearchCost", research.TechTreeFirstUnlockCost, player);
            else
                cost = research.TechTreeFirstUnlockCost > 0 ? research.TechTreeFirstUnlockCost : 0;

            if (cost == 0)
                return null;

            player.inventory.Take(null, itemid, cost);
            player.blueprints.Unlock(node.itemDef);
            Interface.CallHook("OnTechTreeNodeUnlocked", workbench, node, player);
            CuiHelper.DestroyUi(player, "Tree");
            return false;
        }

        #endregion

        #region CurrencySystems / Misc Helpers

        // Set || Reset Research Table Requirements
        private void UpdateResearchTables(string shortname = "scrap")
        {
            foreach (ResearchTable table in BaseNetworkable.serverEntities.OfType<ResearchTable>())
            {
                var i = ItemManager.FindItemDefinition(shortname);
                if (i != null)
                    table.researchResource = i;
            }
        }

        public Research FindResearch(string item)
        {
            Research research;
            return _confg.ResearchSettings.TryGetValue(item, out research) ? research : null;
        }

        public int PriceRounder(double amount)
        {
            if (amount <= 0.5)
            {
                return (int)Math.Ceiling(amount);
            }

            return (int)Math.Round(amount);
        }

        private void AddCurrency(BasePlayer player, double amount)
        {
            if (_confg.Economics && Economics != null)
            {
                Economics?.Call("Deposit", player.UserIDString, amount);
            }

            if (_confg.ServerRewards && ServerRewards != null)
            {
                ServerRewards?.Call("AddPoints", player.UserIDString, PriceRounder(amount));
            }

            if (_confg.Custom)
            {
                Item currency = ItemManager.CreateByItemID(_confg.CId, PriceRounder(amount), _confg.CSkinId);
                if (!_confg.CName.IsNullOrEmpty())
                {
                    currency.name = _confg.CName;
                    currency.MarkDirty();
                }

                player.GiveItem(currency);
            }
        }

        private bool GiveCurrency(BasePlayer player, string shortname)
        {
            Research item = FindResearch(shortname);

            if (item == null)
                return false;

            if (_confg.Economics && Economics != null)
            {
                AddCurrency(player, item.EarnCurrencyAmount);
                PopupMessage(player.GetPlayer(), EasyResearchLang("AddedCurrency", player.UserIDString, item.EarnCurrencyAmount));
                return true;
            }

            if (_confg.ServerRewards && ServerRewards != null)
            {
                AddCurrency(player, item.EarnCurrencyAmount);
                PopupMessage(player.GetPlayer(), EasyResearchLang("AddedCurrency", player.UserIDString, item.EarnCurrencyAmount));
                return true;
            }

            if (!_confg.Custom) return false;
            AddCurrency(player, item.EarnCurrencyAmount);
            PopupMessage(player.GetPlayer(), EasyResearchLang("AddedCurrency", player.UserIDString, item.EarnCurrencyAmount));
            return true;
        }

        #endregion

        #region UIHelpers

        public class Rectangle
        {
            public decimal Height = 1;
            public decimal Width = 1;
            public decimal X;
            public decimal Y;

            public Rectangle() { }

            public Rectangle(decimal x, decimal y, decimal width, decimal height)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
            }

            public string GetMinAnchor() => $"{Math.Max(0, X)} {Math.Max(0, 1 - Y - Height)}";

            public string GetMaxAnchor() => $"{Math.Min(1, X + Width)} {Math.Min(1, 1 - Y)}";

            public string[] ToAnchors()
            {
                return new[]
                {
                    GetMinAnchor(),
                    GetMaxAnchor(),
                };
            }
        }

        public void PanelUI1(ref CuiElementContainer container, string parent, string name, string min, string max, string offmin, string offmax, string bgColor, bool curser = false)
        {
            container.Add(new CuiPanel
            {
                CursorEnabled = curser,
                Image =
                {
                    Color = bgColor
                },
                RectTransform =
                {
                    AnchorMin = min,
                    AnchorMax = max,
                    OffsetMin = offmin,
                    OffsetMax = offmax,
                }
            }, parent, name);
        }

        public void PanelUI2(ref CuiElementContainer container, string parent, string name, string min, string max, string bgColor, bool curser = false)
        {
            container.Add(new CuiPanel
            {
                CursorEnabled = curser,
                Image =
                {
                    Color = bgColor
                },
                RectTransform =
                {
                    AnchorMin = min,
                    AnchorMax = max,
                }
            }, parent, name);
        }

        public void LabelUI1(ref CuiElementContainer container, string parent, string name, Rectangle rectangle, string textColor, string text, int fontSize = 14, TextAnchor textAnchor = TextAnchor.MiddleCenter, bool fontbold = false)
        {
            container.Add(new CuiLabel
            {
                Text = {
                    Text = text,
                    FontSize = fontSize,
                    Color = textColor,
                    Align = textAnchor,
                    Font = fontbold ? "RobotoCondensed-Bold.ttf" : "RobotoCondensed-Regular.ttf"
                },
                RectTransform =
                {
                   AnchorMin = rectangle.GetMinAnchor(),
                   AnchorMax = rectangle.GetMaxAnchor(),
                }
            }, parent, name);
        }

        public void LableUI2(ref CuiElementContainer container, string parent, string name, string textColor, string text, int fontSize, string anchorMin, string anchorMax, TextAnchor align = TextAnchor.MiddleCenter, float fadeIn = 0f)
        {
            container.Add(new CuiLabel
            {
                Text =
                {
                    Color = textColor, 
                    FontSize = fontSize, 
                    Align = align, 
                    Text = text, 
                    FadeIn = fadeIn
                },
                RectTransform =
                {
                    AnchorMin = anchorMin, 
                    AnchorMax = anchorMax
                }
            }, parent, name);
        }

        private void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player,"Main");
            CuiHelper.DestroyUi(player, "Status");
            CuiHelper.DestroyUi(player, "RCost");
            CuiHelper.DestroyUi(player, "Button");
            CuiHelper.DestroyUi(player, "Tree");
        }

        #endregion

        #region Color Helper

        public class Color
        {
            public string HexColor;
            public float Alpha = 1f;

            public Color(string hexColor, float alpha = 1f)
            {
                HexColor = hexColor;
                Alpha = alpha;
            }

            public static string ToRGB(string hexColor, float alpha = 1f)
            {
                if (hexColor.StartsWith("#"))
                    hexColor = hexColor.TrimStart('#');
                int red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255} {alpha}";
            }

            public string ToRGB() => ToRGB(HexColor, Alpha);
        }

        #endregion
    }

    namespace EasyResearchEx
    {
        public static class PlayerEx
        {
            //public static BasePlayer GetPlayer2(this IPlayer player) => player?.Object as BasePlayer;
            public static IPlayer GetPlayer( this BasePlayer player) => player.IPlayer;
        }   
    }
}