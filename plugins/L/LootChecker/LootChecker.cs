using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Loot Checker", "Rustoholics", "0.1.0")]
    [Description("View a summary of all the loot in crates and barrels")]

    public class LootChecker : CovalencePlugin
    {
        #region Variables

        private int DefaultPrefab = 4;
        private List<string> _prefabs = new List<string>
        {
            "assets/bundled/prefabs/radtown/crate_basic.prefab",
            "assets/bundled/prefabs/radtown/crate_elite.prefab",
            "assets/bundled/prefabs/radtown/crate_mine.prefab",
            "assets/bundled/prefabs/radtown/crate_normal.prefab",
            "assets/bundled/prefabs/radtown/crate_normal_2.prefab",
            "assets/bundled/prefabs/radtown/crate_normal_2_food.prefab",
            "assets/bundled/prefabs/radtown/crate_normal_2_medical.prefab",
            "assets/bundled/prefabs/radtown/crate_tools.prefab",
            "assets/bundled/prefabs/radtown/crate_underwater_advanced.prefab",
            "assets/bundled/prefabs/radtown/crate_underwater_basic.prefab",
            "assets/bundled/prefabs/radtown/dmloot/dm ammo.prefab",
            "assets/bundled/prefabs/radtown/dmloot/dm c4.prefab",
            "assets/bundled/prefabs/radtown/dmloot/dm construction resources.prefab",
            "assets/bundled/prefabs/radtown/dmloot/dm construction tools.prefab",
            "assets/bundled/prefabs/radtown/dmloot/dm food.prefab",
            "assets/bundled/prefabs/radtown/dmloot/dm medical.prefab",
            "assets/bundled/prefabs/radtown/dmloot/dm res.prefab",
            "assets/bundled/prefabs/radtown/dmloot/dm tier1 lootbox.prefab",
            "assets/bundled/prefabs/radtown/dmloot/dm tier2 lootbox.prefab",
            "assets/bundled/prefabs/radtown/dmloot/dm tier3 lootbox.prefab",
            "assets/bundled/prefabs/radtown/vehicle_parts.prefab",
            "assets/bundled/prefabs/radtown/foodbox.prefab",
            "assets/bundled/prefabs/radtown/loot_barrel_1.prefab",
            "assets/bundled/prefabs/radtown/loot_barrel_2.prefab",
            "assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab",
            "assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-2.prefab",
            "assets/bundled/prefabs/autospawn/resource/loot/trash-pile-1.prefab",
            "assets/bundled/prefabs/radtown/loot_trash.prefab",
            "assets/bundled/prefabs/radtown/minecart.prefab",
            "assets/bundled/prefabs/radtown/oil_barrel.prefab",
            "assets/prefabs/npc/m2bradley/bradley_crate.prefab",
            "assets/prefabs/npc/patrol helicopter/heli_crate.prefab",
            "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab",
            "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate_oilrig.prefab",
            "assets/prefabs/misc/supply drop/supply_drop.prefab",
            "assets/bundled/prefabs/radtown/underwater_labs/crate_normal.prefab",
            "assets/bundled/prefabs/radtown/underwater_labs/crate_normal_2.prefab",
            "assets/bundled/prefabs/radtown/underwater_labs/crate_elite.prefab",
            "assets/bundled/prefabs/radtown/underwater_labs/crate_tools.prefab",
            "assets/bundled/prefabs/radtown/underwater_labs/crate_food_2.prefab",
            "assets/bundled/prefabs/radtown/underwater_labs/crate_ammunition.prefab",
            "assets/bundled/prefabs/radtown/underwater_labs/crate_medical.prefab",
            "assets/bundled/prefabs/radtown/underwater_labs/crate_fuel.prefab",
            "assets/bundled/prefabs/radtown/underwater_labs/tech_parts_2.prefab",
            //"assets/prefabs/npc/scientist/scientist_corpse.prefab"
        };
        
        #endregion

        #region Permissions

        private void Init()
        {
            permission.RegisterPermission("lootchecker.use", this);
        }
        
        #endregion


        #region Language
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Item"] = "Item",
                ["Count"] = "Count",
                ["Percent"] = "Percent",
                ["Total"] = "Total",
                ["Average"] = "Average",
                ["LootExample"] = "For example: `loot.view {0}` command will show the loot for {1}",
                ["HelpMessage"] = "Below are list of available loot container types, enter the INDEX number with the loot.view command to view the loot."
            }, this);
        }
        
        #endregion


        #region Helpers

        private string GetPrefabName(string prefab)
        {
            TextInfo textInfo = new CultureInfo("en-US",false).TextInfo;
            return textInfo.ToTitleCase(prefab.Split('/').Last().Split('.').First().Replace('_',' ').Replace('-',' '));
        }
        
        private static string ToFixedLength(object obj, int length, char pad=' ')
        {
            var str = Convert.ToString(obj);
            if (str == null) return "";
            if (str.Length > length) str = str.Substring(0, length);
            return str.PadRight(length, pad);
        }
        #endregion


        #region Commands

        [Command("loot.help"), Permission("lootchecker.use")]
        private void ListPrefabsCommand(IPlayer iplayer, string command, string[] args)
        {
            iplayer.Reply(Lang("HelpMessage",iplayer.Id));
            iplayer.Reply(Lang("LootExample", iplayer.Id, DefaultPrefab, GetPrefabName(_prefabs[DefaultPrefab])));
            for (var x = 0; x < _prefabs.Count; x++)
            {
                iplayer.Reply($"{x} : {GetPrefabName(_prefabs[x])}");
            }
        }

        [Command("loot.view"), Permission("lootchecker.use")]
        private void ViewLootCommand(IPlayer iplayer, string command, string[] args)
        {
            int pindex;
            if (args.Length == 0 || !Int32.TryParse(args[0], out pindex))
            {
                pindex = DefaultPrefab;
            }
            
            var spawns = Resources.FindObjectsOfTypeAll<LootContainer>()
                .Where(c => c.PrefabName == _prefabs[pindex])
                .Where(c => c.isActiveAndEnabled)
                .ToList();
            
            Dictionary<string, int> LootQty = new Dictionary<string, int>();
            Dictionary<string, int> LootCount = new Dictionary<string, int>();
            
            foreach (var s in spawns)
            {
                if (s.inventory == null || s.inventory.itemList.Count == 0) continue;

                foreach (var item in s.inventory.itemList)
                {
                    if (item.amount <= 0) continue;
                    
                    if (!LootQty.ContainsKey(item.info.shortname))
                    {
                        LootQty.Add(item.info.shortname, item.amount);
                        LootCount.Add(item.info.shortname, 1);
                    }
                    else
                    {
                        LootQty[item.info.shortname] += item.amount;
                        LootCount[item.info.shortname]++;
                    }
                }
            }
            
            iplayer.Reply($"{GetPrefabName(_prefabs[pindex])} | {Lang("Total", iplayer.Id)} {spawns.Count}");
            
            iplayer.Reply($"| {ToFixedLength(Lang("Item", iplayer.Id),36)}| {ToFixedLength(Lang("Count", iplayer.Id),8)}| {ToFixedLength(Lang("Percent", iplayer.Id),10)}| {ToFixedLength(Lang("Total", iplayer.Id),10)}| {ToFixedLength(Lang("Average", iplayer.Id),10)}");
            iplayer.Reply($"|-{ToFixedLength("-", 36,'-')}|-{ToFixedLength("-", 8,'-')}|-{ToFixedLength("-", 10,'-')}|-{ToFixedLength("-", 10,'-')}|-{ToFixedLength("-", 10,'-')}");

            foreach (var list in LootCount.OrderByDescending(c => c.Value))
            {
                iplayer.Reply($"| {ToFixedLength(list.Key, 36)}| {ToFixedLength(list.Value, 8)}| {ToFixedLength(Math.Round(((double)list.Value / spawns.Count) * 100, 2), 10)}| {ToFixedLength(LootQty[list.Key], 10)}| {ToFixedLength(Math.Round(((double)LootQty[list.Key] / list.Value), 2), 10)}");
            }
        }

        #endregion

    }
}