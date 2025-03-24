/* --- Contributor information ---
 * Please follow the following set of guidelines when working on this plugin,
 * this to help others understand this file more easily.
 *
 * NOTE: On Authors, new entries go BELOW the existing entries. As with any other software header comment.
 *
 * -- Authors --
 * Thimo (ThibmoRozier) <thibmorozier@live.nl> 2021-04-24 +
 *
 * -- Naming --
 * Avoid using non-alphabetic characters, eg: _
 * Avoid using numbers in method and class names (Upgrade methods are allowed to have these, for readability)
 * Private constants -------------------- SHOULD start with a uppercase "C" (PascalCase)
 * Private readonly fields -------------- SHOULD start with a uppercase "C" (PascalCase)
 * Private fields ----------------------- SHOULD start with a uppercase "F" (PascalCase)
 * Arguments/Parameters ----------------- SHOULD start with a lowercase "a" (camelCase)
 * Classes ------------------------------ SHOULD start with a uppercase character (PascalCase)
 * Methods ------------------------------ SHOULD start with a uppercase character (PascalCase)
 * Public properties (constants/fields) - SHOULD start with a uppercase character (PascalCase)
 * Variables ---------------------------- SHOULD start with a lowercase character (camelCase)
 *
 * -- Style --
 * Max-line-width ------- 160
 * Single-line comments - // Single-line comment
 * Multi-line comments -- Just like this comment block!
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Item Skin Setter", "ThibmoRozier", "1.1.6")]
    [Description("Sets the default skin ID for newly crafted items.")]
    public class ItemSkinSetter : RustPlugin
    {
        #region Types
        private struct ShortnameToWorkshopId
        {
            [JsonProperty("Item Shortname")]
            public string ItemShortname;
            [JsonProperty("Skin Id")]
            public ulong SkinId;
        }

        /// <summary>
        /// The config type class
        /// </summary>
        private class ConfigData
        {
            [JsonProperty("Bindings")]
            public List<ShortnameToWorkshopId> Bindings = new List<ShortnameToWorkshopId>();
        }
        #endregion Types

        #region Variables
        private ConfigData FConfigData;
        private Dictionary<int, ulong> FItemSkinBindings;
        #endregion Variables

        #region Script Methods
        private string _(string aKey, string aPlayerId = null) => lang.GetMessage(aKey, this, aPlayerId);

        /// <summary>
        /// Determine whether or not a given string is a number
        /// </summary>
        /// <param name="aStr"></param>
        /// <returns></returns>
        private bool IsNumber(string aStr)
        {
            if (String.IsNullOrEmpty(aStr))
                return false;

            char cur;

            for (int i = 0; i < aStr.Length; i++)
            {
                cur = aStr[i];

                if (cur.Equals('-') || cur.Equals('+'))
                    continue;

                if (Char.IsDigit(cur) == false)
                    return false;
            }

            return true;
        }

        private void PerformStartupConfigCheck()
        {
            // We need the SteamPlatform ItemDefinitions to be there, if it's not we get a null-ref exception
            if ((!PlatformService.Instance.IsValid) || PlatformService.Instance.ItemDefinitions == null)
            {
                // Retry in one second
                timer.Once(1f, PerformStartupConfigCheck);
                return;
            }

            ShortnameToWorkshopId confItem;
            ItemDefinition itemDef;
            StringBuilder sb = new StringBuilder("Config parsing errors:\n");
            int errCount = 0;

            for (int i = 0; i < FConfigData.Bindings.Count; i++)
            {
                confItem = FConfigData.Bindings[i];

                if (confItem.SkinId == 0)
                    continue;

                itemDef = ItemManager.FindItemDefinition(confItem.ItemShortname);

                if (itemDef == null)
                {
                    sb.Append("  - " + String.Format(_("Err Item Does Not Exist"), confItem.ItemShortname) + "\n");
                    errCount++;
                    continue;
                }

                if (!itemDef.HasSkins)
                {
                    sb.Append("  - " + String.Format(_("Err Skin Does Not Exist"), confItem.ItemShortname) + "\n");
                    errCount++;
                    continue;
                }

                FItemSkinBindings[itemDef.itemid] = confItem.SkinId;
                Puts($"Item {itemDef.shortname} skin set to {FItemSkinBindings[itemDef.itemid]}");
            }

            if (errCount > 0)
                Puts(sb.Append($"\nTotal error count: {errCount}\n").ToString());
        }
        #endregion Script Methods

        #region Hooks
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(
                new Dictionary<string, string> {
                    { "Err Invalid Args", "Invalid argument (count), please try again." },
                    { "Err Invalid Permission", "You do not have permission to use this command." },
                    { "Err Item Does Not Exist", "Item \"{0}\" does not exist." },
                    { "Err Skin Does Not Exist", "Skin with ID \"{0}\" does not exist." },

                    { "Msg Item Skin Default", "The skin of item \"{0}\" ({1}) is default." },
                    { "Msg Item Skin", "The skin of item \"{0}\" ({1}) is \"{2}\" ( Name = \"{3}\" )." }
                }, this, "en"
            );
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            FConfigData = Config.ReadObject<ConfigData>();

            if (FConfigData == null)
                LoadDefaultConfig();
        }

        protected override void LoadDefaultConfig()
        {
            FConfigData = new ConfigData();
            List<ItemDefinition> itemlist = ItemManager.GetItemDefinitions();
            ItemDefinition curItem;

            for (int i = 0; i < itemlist.Count; i++)
            {
                curItem = itemlist[i];

                if (curItem.HasSkins)
                    FConfigData.Bindings.Add(new ShortnameToWorkshopId { ItemShortname = curItem.shortname, SkinId = 0 });
            }

            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(FConfigData);

        void OnServerInitialized()
        {
            FItemSkinBindings = new Dictionary<int, ulong>();
            PerformStartupConfigCheck();
        }

        void OnItemCraftFinished(ItemCraftTask aTask, Item aItem)
        {
            if (aTask?.skinID == 0 && FItemSkinBindings.ContainsKey(aTask.blueprint.targetItem.itemid))
            {
                aItem.skin = FItemSkinBindings[aTask.blueprint.targetItem.itemid];
                aItem.MarkDirty();
            }
        }
        #endregion Hooks

        #region Commands
        [ConsoleCommand("iss_get")]
        private void ItemSkinSetterGetCmd(ConsoleSystem.Arg aArg)
        {
            if (aArg.IsClientside)
            {
                aArg.ReplyWith(_("Err Invalid Permission", aArg.Connection.userid.ToString()));
                return;
            }

            if (aArg.Args.Length < 1)
            {
                Puts(_("Err Invalid Args"));
                return;
            }

            string itemArg = aArg.Args[0];
            ItemDefinition itemDef = null;

            if (!IsNumber(itemArg))
            {
                itemDef = ItemManager.FindItemDefinition(itemArg);
            }
            else
            {
                int itemId;

                if (int.TryParse(itemArg, out itemId))
                    itemDef = ItemManager.FindItemDefinition(itemId);
            }

            if (itemDef == null)
                Puts(_("Err Item Does Not Exist"), itemArg);

            if (FItemSkinBindings.ContainsKey(itemDef.itemid))
            {
                ulong skinId = FItemSkinBindings[itemDef.itemid];
                IPlayerItemDefinition skinDef = itemDef.skins2.First(x => x.WorkshopId == skinId);
                Puts(_("Msg Item Skin"), itemDef.shortname, itemDef.itemid, skinDef.WorkshopId, skinDef.Name);
            }
            else
            {
                Puts(_("Msg Item Skin Default"), itemDef.shortname, itemDef.itemid);
            }
        }

        [ConsoleCommand("iss_getskins")]
        private void ItemSkinSetterGetSkinsCmd(ConsoleSystem.Arg aArg)
        {
            if (aArg.IsClientside)
            {
                aArg.ReplyWith(_("Err Invalid Permission", aArg.Connection.userid.ToString()));
                return;
            }

            if (aArg.Args.Length < 1)
            {
                Puts(_("Err Invalid Args"));
                return;
            }

            string itemArg = aArg.Args[0];
            ItemDefinition itemDef = null;

            if (!IsNumber(itemArg))
            {
                itemDef = ItemManager.FindItemDefinition(itemArg);
            }
            else
            {
                int itemId;

                if (int.TryParse(itemArg, out itemId))
                    itemDef = ItemManager.FindItemDefinition(itemId);
            }

            if (itemDef == null)
                Puts(_("Err Item Does Not Exist"), itemArg);

            IPlayerItemDefinition[] skinDefs = itemDef.skins2;
            StringBuilder sb = new StringBuilder($"Skins for item \"{itemDef.shortname}\" ({itemDef.itemid})\n");

            for (int i = 0; i < skinDefs.Length; i++)
                sb.Append($"  - {skinDefs[i].Name} ({skinDefs[i].WorkshopId})\n");

            Puts(sb.ToString());
        }
        #endregion Commands
    }
}
