using System.Linq;
using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;

#region License
/*
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), 
to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, 
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, 
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
#endregion

namespace Oxide.Plugins
{
    [Info("Armor Not Forever", "Flames", "1.0.10")]
    [Description("On hit NPC to not only deal damage to the player, but also reduce the durability of equipped armor")]

    class ArmorNotForever : RustPlugin
    {
        #region Permission

        private const string PermissionUse = "armornotforever.use";
        private const string PermissionDamageTotal = "armornotforever.damagetotal";
        private const string PermissionDamageMultiplier = "armornotforever.damagemultiplier";
        private const string PermissionDamageBypass = "armornotforever.damagebypass";

        #endregion

        #region Configuration
        private ConfigData _configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Global settings")]
            public GlobalSettings globalSettings = new GlobalSettings();

            [JsonProperty(PropertyName = "Multiplier settings")]
            public MultiplierSettings multiplierSettings = new MultiplierSettings();

            public class GlobalSettings
            {
                [JsonProperty(PropertyName = "Debag")]
                public bool Debag;

                [JsonProperty(PropertyName = "Damage from (Suicide). Does not damage armor.")]
                public bool DamageTypeSuicide;

                [JsonProperty(PropertyName = "Damage from (Bleeding). Does not damage armor.")]
                public bool DamageTypeBleeding;

                [JsonProperty(PropertyName = "Damage from (Drowning). Does not damage armor.")]
                public bool DamageTypeDrowning;

                [JsonProperty(PropertyName = "Damage from (Thirst). Does not damage armor.")]
                public bool DamageTypeThirst;

                [JsonProperty(PropertyName = "Damage from (Hunger). Does not damage armor.")]
                public bool DamageTypeHunger;

                [JsonProperty(PropertyName = "Damage from (Cold). Does not damage armor.")]
                public bool DamageTypeCold;

                [JsonProperty(PropertyName = "Damage from (Heat). Does not damage armor.")]
                public bool DamageTypeHeat;

                [JsonProperty(PropertyName = "Damage from (Fall). Does not damage armor.")]
                public bool DamageTypeFall;

                [JsonProperty(PropertyName = "Damage from (Radiation). Does not damage armor.")]
                public bool DamageTypeRadiation;
            }

            public class MultiplierSettings
            {
                [JsonProperty(PropertyName = "Item list Head (item shortname : multiplier)")]
                public Dictionary<string, float> list_Head = new Dictionary<string, float>()
                {
                    ["metal.facemask"] = 1.0f,
                    ["diving.mask"] = 1.0f,
                    ["hat.gas.mask"] = 1.0f,
                    ["heavy.plate.helmet"] = 1.0f,
                    ["bucket.helmet"] = 1.0f,
                    ["wood.armor.helmet"] = 1.0f,
                    ["sunglasses"] = 1.0f,
                    ["twitchsunglasses"] = 1.0f,
                    ["riot.helmet"] = 1.0f,
                    ["coffeecan.helmet"] = 1.0f,
                    ["deer.skull.mask"] = 1.0f,
                };

                [JsonProperty(PropertyName = "Item list Body (item shortname : multiplier)")]
                public Dictionary<string, float> list_Body = new Dictionary<string, float>()
                {
                    ["hazmatsuit"] = 1.0f,
                    ["hazmatsuit.arcticsuit"] = 1.0f,
                    ["hazmatsuit.nomadsuit"] = 1.0f,
                    ["hazmatsuit.spacesuit"] = 1.0f,
                    ["heavy.plate.jacket"] = 1.0f,
                    ["metal.plate.torso"] = 1.0f,
                    ["roadsign.jacket"] = 1.0f,
                    ["bone.armor.suit"] = 1.0f,
                    ["wood.armor.jacket"] = 1.0f,
                    ["attire.hide.poncho"] = 1.0f,
                    ["jumpsuit.suit"] = 1.0f,
                    ["jumpsuit.suit.blue"] = 1.0f,
                    ["cratecostume"] = 1.0f,
                    ["barrelcostume"] = 1.0f,
                    ["gloweyes"] = 1.0f,
                };

                [JsonProperty(PropertyName = "Item list Pants (item shortname : multiplier)")]
                public Dictionary<string, float> list_Pants = new Dictionary<string, float>()
                {
                    ["heavy.plate.pants"] = 1.0f,
                    ["wood.armor.pants"] = 1.0f,
                    ["roadsign.kilt"] = 1.0f,
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _configData = Config.ReadObject<ConfigData>();
                if (_configData == null) LoadDefaultConfig();
            }
            catch
            {
                PrintError("The configuration file is corrupted");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            _configData = new ConfigData();
        }

        protected override void SaveConfig() => Config.WriteObject(_configData);

        #endregion Configuration


        #region Init

        private void Init()
        {
            permission.RegisterPermission(PermissionUse, this);
            permission.RegisterPermission(PermissionDamageTotal, this);
            permission.RegisterPermission(PermissionDamageMultiplier, this);
            permission.RegisterPermission(PermissionDamageBypass, this);
        }

        #endregion

        #region Unload

        void Unload()
        {
            _configData = null;
        }

        #endregion

        #region OnEntityTakeDamage

        void OnEntityTakeDamage(BasePlayer player, HitInfo info)
        {
            if (player == null || !player.userID.IsSteamId() || player.IsNpc || info == null || CheckDamageType(info) || !permission.UserHasPermission(player.UserIDString, PermissionUse) || permission.UserHasPermission(player.UserIDString, PermissionDamageBypass)) return;

            var damageTotal = info.damageTypes.Total();

            foreach (Item item in player.inventory.containerWear.itemList.ToList())
            {
                if (item.conditionNormalized == 0)
                {
                    Message(player, "ArmorBroken");
                    Effect.server.Run("assets/bundled/prefabs/fx/item_break.prefab", player.transform.position);

                    if (24 - player.inventory.containerMain.itemList.Count > 0)
                    {
                        item.MoveToContainer(player.inventory.containerMain);
                    }
                    else
                    {
                        item.Drop(player.transform.position, Vector3.up);
                        Message(player, "InventoryFull");
                    }
                    break;
                }

                foreach (var itemHead in _configData.multiplierSettings.list_Head)
                {
                    if (GetRandom() && item.info.shortname == itemHead.Key)
                    {
                        if (permission.UserHasPermission(player.UserIDString, PermissionDamageTotal) && !permission.UserHasPermission(player.UserIDString, PermissionDamageMultiplier))
                        {
                            item.condition -= damageTotal;
                            if (_configData.globalSettings.Debag) Puts($"Item {itemHead.Key} damageTotal {damageTotal}");
                        }
                        
                        if (permission.UserHasPermission(player.UserIDString, PermissionDamageMultiplier))
                        {
                            item.condition -= itemHead.Value;
                            if (_configData.globalSettings.Debag) Puts($"Item {itemHead.Key} damageMultiplier {itemHead.Value}");
                        }
                    }
                }

                foreach (var itemBody in _configData.multiplierSettings.list_Body)
                {
                    if (GetRandom() && item.info.shortname == itemBody.Key)
                    {
                        if (permission.UserHasPermission(player.UserIDString, PermissionDamageTotal) && !permission.UserHasPermission(player.UserIDString, PermissionDamageMultiplier))
                        {
                            item.condition -= damageTotal;
                            if (_configData.globalSettings.Debag) Puts($"Item {itemBody.Key} damageTotal {damageTotal}");
                        }
                        
                        if (permission.UserHasPermission(player.UserIDString, PermissionDamageMultiplier))
                        {
                            item.condition -= itemBody.Value;
                            if (_configData.globalSettings.Debag) Puts($"Item {itemBody.Key} damageMultiplier {itemBody.Value}");
                        }
                    }
                }

                foreach (var itemPants in _configData.multiplierSettings.list_Pants)
                {
                    if (GetRandom() && item.info.shortname == itemPants.Key)
                    {
                        if (permission.UserHasPermission(player.UserIDString, PermissionDamageTotal) && !permission.UserHasPermission(player.UserIDString, PermissionDamageMultiplier))
                        {
                            item.condition -= damageTotal;
                            if (_configData.globalSettings.Debag) Puts($"Item {itemPants.Key} damageTotal {damageTotal}");
                        }
                        
                        if (permission.UserHasPermission(player.UserIDString, PermissionDamageMultiplier))
                        {
                            item.condition -= itemPants.Value;
                            if (_configData.globalSettings.Debag) Puts($"Item {itemPants.Key} damageMultiplier {itemPants.Value}");
                        }
                    }
                }
            }
        }

        #endregion

        #region CanWearItem

        object CanWearItem(PlayerInventory inventory, Item item, int targetSlot)
        {
            var player = inventory.GetComponent<BasePlayer>();
            if (item.conditionNormalized == 0)
            {
                Message(player, "ItemBroken");
                Effect.server.Run("assets/prefabs/npc/autoturret/effects/targetlost.prefab", player.transform.position);
                return false;
            }
            return null;
        }

        #endregion

        #region Helper Damage

        private bool CheckDamageType(HitInfo info)
        {
            if (_configData.globalSettings.DamageTypeSuicide && info.damageTypes.Has(Rust.DamageType.Suicide)
            || _configData.globalSettings.DamageTypeBleeding && info.damageTypes.Has(Rust.DamageType.Bleeding)
            || _configData.globalSettings.DamageTypeDrowning && info.damageTypes.Has(Rust.DamageType.Drowned)
            || _configData.globalSettings.DamageTypeThirst && info.damageTypes.Has(Rust.DamageType.Thirst)
            || _configData.globalSettings.DamageTypeHunger && info.damageTypes.Has(Rust.DamageType.Hunger)
            || _configData.globalSettings.DamageTypeCold && info.damageTypes.Has(Rust.DamageType.Cold)
            || _configData.globalSettings.DamageTypeHeat && info.damageTypes.Has(Rust.DamageType.Heat)
            || _configData.globalSettings.DamageTypeFall && info.damageTypes.Has(Rust.DamageType.Fall)
            || _configData.globalSettings.DamageTypeRadiation && info.damageTypes.Has(Rust.DamageType.Radiation))
            {
                return true;
            }

            return false;
        }

        private static bool GetRandom()
        {
            return UnityEngine.Random.Range(0f, 1f) < 0.5f;
        }

        #endregion

        #region Language

        private void Message(BasePlayer player, string messageKey, params object[] args)
        {
            if (player == null) return;
            var message = GetMessage(messageKey, player.UserIDString, args);
            player.ChatMessage(message);
        }

        private string GetMessage(string messageKey, string playerID, params object[] args)
        {
            return string.Format(lang.GetMessage(messageKey, this, playerID), args);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "ItemBroken", "<color=#ff0000>THIS ITEM IS BROKEN!</color>" },
                { "ArmorBroken", "<color=#ff0000>Your armor has been broken!</color>" },
                { "InventoryFull", "<color=#ff0000>Your inventory has been full! \nThe item has been dropped from your inventory!</color>" },
            }, this, "en");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "ItemBroken", "<color=#ff0000>ЭТОТ ПРЕДМЕТ СЛОМАН!</color>" },
                { "ArmorBroken", "<color=#ff0000>Ваша броня была сломана!</color>" },
                { "InventoryFull", "<color=#ff0000>Ваш инвентарь был переполнен! \nПредмет выпал из вашего инвентаря!</color>" }
            }, this, "ru");
        }

        #endregion Language
    }
}