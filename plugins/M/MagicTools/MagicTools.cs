using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rust;

#region Changelogs and ToDo
/**********************************************************************
 * 
 * v3.0.0   :   Maintained by Krungh Crow
 *          :   Complete rewrite
 *          :   Converted to covalence
 *          :   Added default/vip permissions
 *          :   Extra resources on bonus hit (perms)
 * 
 **********************************************************************/
#endregion

namespace Oxide.Plugins
{
    [Info("Magic Tools", "Krungh Crow", "3.0.0")]
    [Description("Automatically smelts Mined resources and bonuses")]

    class MagicTools : CovalencePlugin
    {

        #region Variables

        const string Use_Perm = "magictools.use";
        const string Default_Perm = "magictools.default";
        const string Vip_Perm = "magictools.vip";

        #endregion

        #region Configuration

        void Init()
        {
            if (!LoadConfigVariables())
            {
            Puts("Config file issue detected. Please delete file, or check syntax and fix.");
            return;
            }

            permission.RegisterPermission(Use_Perm, this);
            permission.RegisterPermission(Default_Perm, this);
            permission.RegisterPermission(Vip_Perm, this);
        }

        private ConfigData configData;

        class ConfigData
        {
            [JsonProperty(PropertyName = "Multiplier on Bonus hit (rounded numbers only(ea 1 not 1.2)")]
            public SettingsBonus Settings = new SettingsBonus();
        }

        class SettingsBonus
        {
            [JsonProperty(PropertyName = "Default multiplier on Bonus")]
            public int MultiDefault = 2;
            [JsonProperty(PropertyName = "Vip multiplier on Bonus")]
            public int MultiVip = 3;
        }

        private bool LoadConfigVariables()
        {
            try
            {
            configData = Config.ReadObject<ConfigData>();
            }
            catch
            {
            return false;
            }
            SaveConf();
            return true;
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Fresh install detected Creating a new config file.");
            configData = new ConfigData();
            SaveConf();
        }

        void SaveConf() => Config.WriteObject(configData, true);
        #endregion

        #region Hooks

        private object OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (permission.UserHasPermission(player.UserIDString, Use_Perm))
            {
                if (dispenser == null || player == null || item == null)
                    return null;

                ItemModCookable cookable = item.info.GetComponent<ItemModCookable>();
                if (cookable == null)
                    return null;

                var cookedItem = ItemManager.Create(cookable.becomeOnCooked, item.amount);
                player.GiveItem(cookedItem, BaseEntity.GiveItemReason.ResourceHarvested);

                return true;
            }
            return null;

        }
        private object OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (permission.UserHasPermission(player.UserIDString, Use_Perm))
            {
                if (dispenser == null || player == null || item == null)
                    return null;

                ItemModCookable cookable = item.info.GetComponent<ItemModCookable>();
                if (cookable == null)
                    return null;

                if (permission.UserHasPermission(player.UserIDString, Vip_Perm))
                {
                    item.amount = item.amount * configData.Settings.MultiVip;
                }

                else if (permission.UserHasPermission(player.UserIDString, Default_Perm))
                {
                    item.amount = item.amount * configData.Settings.MultiDefault;
                }

                var cookedItem = ItemManager.Create(cookable.becomeOnCooked, item.amount);
                player.GiveItem(cookedItem, BaseEntity.GiveItemReason.ResourceHarvested);
                NextTick(() =>
                {
                    item.DoRemove();
                });
                return true;
            }
            return null;
        }

        #endregion
    }
}