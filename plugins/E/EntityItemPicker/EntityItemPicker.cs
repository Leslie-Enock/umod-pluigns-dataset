using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UnityEngine;

using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Entity Item Picker", "0x89A", "1.0.1")]
    [Description("Get the item of any deployable with a single button press")]
    class EntityItemPicker : RustPlugin
    {
        private Configuration _config;
        
        private const string use = "entityitempicker.use";
        private const string bypassCooldown = "entityitempicker.bypass";

        private Regex _removeRegex = new Regex(".prefab|.deployed|.entity|_deployed|_leather|electrical.|electric.");

        private Dictionary<string, string> _prefabToItemShortname = new Dictionary<string, string>
        {
            ["sign.huge.wood.prefab"] = "sign.wooden.huge",
            ["sign.large.wood.prefab"] = "sign.wooden.large",
            ["sign.medium.wood.prefab"] = "sign.wooden.medium",
            ["sign.small.wood.prefab"] = "sign.wooden.small",
            ["wall.external.high.wood.prefab"] = "wall.external.high",
            ["survivalfishtrap.deployed.prefab"] = "fishtrap.small",
            ["waterpurifier.deployed.prefab"] = "water.purifier",
            ["refinery_small_deployed.prefab"] = "small.oil.refinery",
            ["barricade.cover.wood.prefab"] = "barricade.wood.cover"
        };

        private Dictionary<ulong, float> _lastUseTime = new Dictionary<ulong, float>();

        void Init()
        {
            permission.RegisterPermission(use, this);
            permission.RegisterPermission(bypassCooldown, this);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["InvalidObject"] = "Please look at a valid object",
                ["OnCooldown"] = "Please wait {0} more seconds",
            }, this);
        }

        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (input.WasJustPressed(BUTTON.FIRE_THIRD))
            {
                float useTime = 9999f;
                if (!_lastUseTime.TryGetValue(player.userID, out useTime))
                    _lastUseTime.Add(player.userID, Time.time);

                float time = Time.time - useTime;

                if (permission.UserHasPermission(player.UserIDString, use) && (!_config.useCooldown || permission.UserHasPermission(player.UserIDString, bypassCooldown) || time >= _config.cooldownTime))
                {
                    RaycastHit hit;
                    if (Physics.Raycast(player.eyes.HeadRay(), out hit, _config.maxDist))
                    {
                        BaseCombatEntity entity = null;
                        try
                        {
                            entity = hit.GetEntity() as BaseCombatEntity;
                        }
                        catch
                        {
                            return;
                        }

                        string compareString = _removeRegex.Replace(entity.ShortPrefabName, string.Empty);

                        ItemDefinition itemDef = entity.pickup.itemTarget ?? GetDefinition(compareString, compareString.Replace('_', '.'), compareString.Replace("_", string.Empty), $"electric.{compareString}", GetStringFromDict(entity.ShortPrefabName));

                        if (itemDef != null)
                        {
                            player.GiveItem(ItemManager.Create(itemDef, 1, entity.skinID));

                            if (_config.killWhenShifting && input.IsDown(BUTTON.SPRINT))
                                entity.AdminKill();
                        }
                        else player.ChatMessage(lang.GetMessage("InvalidObject", this, player.UserIDString));

                        _lastUseTime[player.userID] = Time.time;
                    }
                }
                else if (time < _config.cooldownTime)
                {
                    player.ChatMessage(lang.GetMessage("OnCooldown", this, player.UserIDString).Replace("{0}", (_config.cooldownTime - Math.Round(time, 1)).ToString()));
                }
            }
        }

        private ItemDefinition GetDefinition(params string[] comparisons)
        {
            ItemDefinition itemDef = null;

            foreach (string comparison in comparisons)
            {
                if (ItemManager.itemDictionaryByName.TryGetValue(comparison, out itemDef))
                    break;
            }

            return itemDef;
        }

        private string GetStringFromDict(string key)
        {
            string value;
            _prefabToItemShortname.TryGetValue(key, out value);

            return value ?? string.Empty;
        }

        #region -Configuration-

        private class Configuration
        {
            [JsonProperty("Use cooldown")]
            public bool useCooldown = true;

            [JsonProperty("Cooldown duration")]
            public float cooldownTime = 3f;

            [JsonProperty("Maximum distance")]
            public float maxDist = 20f;

            [JsonProperty("Kill on pickup when holding sprint")]
            public bool killWhenShifting = false;
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
                PrintError("Failed to load _config, using default values");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => _config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion
    }
}
