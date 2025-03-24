using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Survey Gather", "MJSU", "1.0.7")]
    [Description("Spawns configurable entities where a player throws a survey charge")]
    internal class SurveyGather : RustPlugin
    {
        #region Class Fields
        [PluginReference] private Plugin GameTipAPI;
        
        private PluginConfig _pluginConfig; //Plugin Config

        private const string AccentColor = "#de8732";
        private const string UsePermission = "surveygather.use";
        private const string AdminPermission = "surveygather.admin";

        private ItemDefinition _surveyCharge;
        
        private readonly Hash<ulong, DateTime> _cooldown = new Hash<ulong, DateTime>();
        private readonly Hash<NetworkableId, RiserBehavior> _behaviors = new Hash<NetworkableId, RiserBehavior>();
        private float _totalChance;

        private static SurveyGather _ins;
        #endregion

        #region Setup & Loading
        private void Init()
        {
            _ins = this;
            HashSet<string> perms = new HashSet<string>();
            perms.Add(UsePermission);
            perms.Add(AdminPermission);

            foreach (string perm in _pluginConfig.Cooldowns.Keys)
            {
                perms.Add(perm);
            }

            foreach (string perm in perms)
            {
                permission.RegisterPermission(perm, this);
            }
            
            cmd.AddChatCommand(_pluginConfig.ChatCommand, this, SurveyGatherChatCommand);
            Unsubscribe(nameof(OnActiveItemChanged));
            _totalChance = _pluginConfig.GatherItems.Sum(gi => gi.Chance);
        }
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [LangKeys.NoPermission] = "You do not have permission to use this command",
                [LangKeys.Chat] = $"<color=#bebebe>[<color={AccentColor}>{Title}</color>] {{0}}</color>",
                [LangKeys.Notification] = "Throw this survey charge on the ground to spawn a random item",
                [LangKeys.NoEntity] = "You're not looking at an entity. Please try to get closer or from a different angle.",
                [LangKeys.Add] = $"You have successfully added <color={AccentColor}>{{0}}</color> prefab to the config.",
                [LangKeys.Cooldown] = "You cannot throw another survey charge for {0} seconds",
                [LangKeys.HelpText] = "Allows admins to configure which entities are spawned with the survey charge.\n" +
                                      $"<color={AccentColor}>/{{0}} add</color> - to add the entity you're looking at\n" +
                                      $"<color={AccentColor}>/{{0}}</color> - to view this help text again\n"
            }, this);
        }
        
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Loading Default Config");
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            Config.Settings.DefaultValueHandling = DefaultValueHandling.Populate;
            _pluginConfig = AdditionalConfig(Config.ReadObject<PluginConfig>());
            Config.WriteObject(_pluginConfig);
        }
        
        private PluginConfig AdditionalConfig(PluginConfig config)
        {
            config.Cooldowns = config.Cooldowns ?? new Hash<string, float>
            {
                [UsePermission] = 0
            };
            config.GatherItems = config.GatherItems ?? new List<GatherConfig>
            {
                new GatherConfig
                {
                    Prefab = "assets/bundled/prefabs/radtown/ore_stone.prefab",
                    Chance = 45f,
                    Distance = 1.5f,
                    Duration = 3f,
                    MinHealth = .3f,
                    MaxHealth = .9f,
                    SaveEntity = false,
                    KillEntityDuration = 3600f
                },
                new GatherConfig
                {
                    Prefab = "assets/bundled/prefabs/radtown/ore_metal.prefab",
                    Chance = 40f,
                    Distance = 1.5f,
                    Duration = 3f,
                    MinHealth = .3f,
                    MaxHealth = .9f,
                    SaveEntity = false,
                    KillEntityDuration = 3600f
                },
                new GatherConfig
                {
                    Prefab = "assets/bundled/prefabs/radtown/ore_sulfur.prefab",
                    Chance = 35f,
                    Distance = 1.5f,
                    Duration = 3f,
                    MinHealth = .3f,
                    MaxHealth = .9f,
                    SaveEntity = false,
                    KillEntityDuration = 3600f
                },
                new GatherConfig
                {
                    Prefab = "assets/bundled/prefabs/radtown/crate_elite.prefab",
                    Chance = 2.5f,
                    Distance = 1.5f,
                    Duration = 5f,
                    MinHealth = 1f,
                    MaxHealth = 1f,
                    SaveEntity = false,
                    KillEntityDuration = 3600f
                },
                new GatherConfig
                {
                    Prefab = "assets/bundled/prefabs/radtown/crate_normal.prefab",
                    Chance = 5,
                    Distance = 1.0f,
                    Duration = 5f,
                    MinHealth = 1f,
                    MaxHealth = 1f,
                    SaveEntity = false,
                    KillEntityDuration = 3600f
                }
            };
            return config;
        }

        private void OnServerInitialized()
        {
            _surveyCharge = ItemManager.FindItemDefinition("surveycharge");
            if (!_pluginConfig.AllowCrafting)
            {
                _surveyCharge.Blueprint.userCraftable = false;
            }

            if (!_pluginConfig.AllowResearching)
            {
                _surveyCharge.Blueprint.isResearchable = false;
            }

            if (!_pluginConfig.EnableNotifications)
            {
                Unsubscribe(nameof(OnActiveItemChanged));
            }

            Subscribe(nameof(OnActiveItemChanged));
        }

        private void Unload()
        {
            if (!_pluginConfig.AllowCrafting)
            {
                _surveyCharge.Blueprint.userCraftable = true;
            }

            if (!_pluginConfig.AllowResearching)
            {
                _surveyCharge.Blueprint.isResearchable = true;
            }

            foreach (RiserBehavior riser in _behaviors.Values.ToList())
            {
                riser.DoDestroy();
            }

            _ins = null;
        }
        #endregion

        #region Chat Command

        private void SurveyGatherChatCommand(BasePlayer player, string cmd, string[] args)
        {
            if (!HasPermission(player, AdminPermission))
            {
                Chat(player, LangKeys.NoPermission);
                return;
            }

            if (args.Length == 0)
            {
                Chat(player, LangKeys.HelpText, _pluginConfig.ChatCommand);
                return;
            }

            switch (args[0].ToLower())
            {
                case "add":
                    HandleAdd(player);
                    break;
                
                default:
                    Chat(player, LangKeys.HelpText, _pluginConfig.ChatCommand);
                    break;
            }
        }

        private void HandleAdd(BasePlayer player)
        {
            BaseEntity entity = Raycast<BaseEntity>(player.eyes.HeadRay(), 5f);
            if (entity == null)
            {
                Chat(player, LangKeys.NoEntity);
                return;
            }
            
            _pluginConfig.GatherItems.Add(new GatherConfig
            {
                Chance = 0.05f,
                Distance = entity.bounds.size.y,
                Duration = 3f,
                MinHealth = 1f,
                MaxHealth = 1f,
                Prefab = entity.PrefabName,
                SaveEntity = false,
                KillEntityDuration = 3600f
            });
            
            Config.WriteObject(_pluginConfig);
            
            Chat(player, LangKeys.Add, entity.PrefabName);
        }
        #endregion

        #region Oxide Hook
        private void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (newItem == null || newItem.info.itemid != _surveyCharge.itemid || !HasPermission(player, UsePermission))
            {
                return;
            }

            Chat(player, LangKeys.Notification);
            GameTipAPI?.Call("ShowGameTip", player, Lang(LangKeys.Notification, player), 6f);
        }

        private void OnExplosiveDropped(BasePlayer player, SurveyCharge entity)
        {
            HandleSurveyCharge(player, entity);
        }

        private void OnExplosiveThrown(BasePlayer player, SurveyCharge entity)
        {
            HandleSurveyCharge(player, entity);
        }

        private void HandleSurveyCharge(BasePlayer player, SurveyCharge charge)
        {
            if (!charge || !HasPermission(player, UsePermission))
            {
                return;
            }

            if (_cooldown.ContainsKey(player.userID) && _cooldown[player.userID] >= DateTime.Now)
            {
                Chat(player, LangKeys.Cooldown, (int)(DateTime.Now - _cooldown[player.userID]).TotalSeconds);
                Item item = player.GetActiveItem();
                if (item.info.itemid == _surveyCharge.itemid)
                {
                    item.amount++;
                }
                
                NextTick(() =>
                {
                    charge.Kill();
                });
                return;
            }

            float cooldown = GetPermissionValue(player, _pluginConfig.Cooldowns, 0f);
            if (cooldown > 0f)
            {
                _cooldown[player.userID] = DateTime.Now + TimeSpan.FromSeconds(cooldown);
            }

            charge.CancelInvoke(charge.Explode);
            charge.Invoke(() =>
            {
                RaycastHit raycastHit;
                if (!WaterLevel.Test(charge.transform.position, true, true, charge) 
                    && TransformUtil.GetGroundInfo(charge.transform.position, out raycastHit, 0.3f, Layers.Terrain) 
                    && !RaycastAny(new Ray(charge.transform.position, Vector3.down), .5f)
                    && !player.IsBuildingBlocked())
                {
                    SpawnEntity(charge.transform.position);
                }
                
                if (charge.explosionEffect.isValid)
                {
                    Effect.server.Run(charge.explosionEffect.resourcePath, charge.PivotPoint(), (!charge.explosionUsesForward ? Vector3.up : charge.transform.forward), null, true);
                }
                
                if (!charge.IsDestroyed)
                {
                    charge.Kill(BaseNetworkable.DestroyMode.Gib);
                }
            }, charge.GetRandomTimerTime());
        }

        private void SpawnEntity(Vector3 pos)
        {
            GatherConfig config = SelectRandomConfig();

            BaseEntity entity = GameManager.server.CreateEntity(config.Prefab, pos + Vector3.down * config.Distance);
            if (!entity)
            {
                return;
            }
            
            if (entity is ResourceEntity)
            {
                ResourceEntity resource = (ResourceEntity)entity;
                resource.health = Core.Random.Range(resource.startHealth * config.MinHealth, resource.startHealth * config.MaxHealth);
                
                if (resource is OreResourceEntity)
                {
                    OreResourceEntity ore = (OreResourceEntity)resource;
                    ore.UpdateNetworkStage();
                }
            }
            else if (entity is BaseCombatEntity)
            {
                BaseCombatEntity combat = (BaseCombatEntity)entity;
                combat.health = Core.Random.Range(combat.startHealth * config.MinHealth, combat.startHealth * config.MaxHealth);
                
                if (combat is LootContainer)
                {
                    LootContainer loot = (LootContainer)combat;
                    loot.minSecondsBetweenRefresh = 0;
                    loot.maxSecondsBetweenRefresh = 0;
                }
            }

            entity.Spawn();
            RiserBehavior riser = entity.gameObject.AddComponent<RiserBehavior>();
            riser.StartRise(config);
            entity.enableSaving = config.SaveEntity;
            entity.SendNetworkUpdate();
            if (!config.SaveEntity && config.KillEntityDuration > 0)
            {
                entity.Invoke(() =>
                {
                    entity.Kill();
                }, config.KillEntityDuration);
            }
        }

        private GatherConfig SelectRandomConfig()
        {
            float random = Core.Random.Range(0, _totalChance);
            float total = 0;
            GatherConfig config = null;
            for (int index = 0; index < _pluginConfig.GatherItems.Count; index++)
            {
                GatherConfig item = _pluginConfig.GatherItems[index];
                if (random <= item.Chance + total)
                {
                    config = item;
                    break;
                }

                total += item.Chance;
            }

            return config ?? _pluginConfig.GatherItems[_pluginConfig.GatherItems.Count - 1];
        }
        #endregion

        #region Helper Methods
        public float GetPermissionValue(BasePlayer player, Hash<string, float> permissions, float defaultValue)
        {
            foreach (KeyValuePair<string,float> perm in permissions.OrderBy(p => p.Value))
            {
                if (HasPermission(player, perm.Key))
                {
                    return perm.Value;
                }
            }

            return defaultValue;
        }

        private readonly RaycastHit[] _results = new RaycastHit[32]; 
        
        private bool RaycastAny(Ray ray, float distance)
        {
            int size = Physics.RaycastNonAlloc(ray, _results, distance);
            for (int i = 0; i < size; i++)
            {
                if (_results[i].GetEntity())
                {
                    return true;
                }
            }

            return false;
        }
        
        private T Raycast<T>(Ray ray, float distance) where T : BaseEntity
        {
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, distance))
            {
                return null;
            }

            return hit.GetEntity() as T;
        }
        
        private void Chat(BasePlayer player, string key, params object[] args) => PrintToChat(player, Lang(LangKeys.Chat, player, Lang(key, player, args)));
        
        private string Lang(string key, BasePlayer player = null, params object[] args)
        {
            try
            {
                return string.Format(lang.GetMessage(key, this, player?.UserIDString), args);
            }
            catch(Exception ex)
            {
                PrintError($"Lang Key '{key}' threw exception\n:{ex}");
                throw;
            }
        }

        private bool HasPermission(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm);
        #endregion

        #region Classes
        private class RiserBehavior : FacepunchBehaviour
        {
            private BaseEntity _entity;
            private GatherConfig _config;
            private float _timeTaken;
            private NetworkableId _id;
            private bool _isCompleted;
            private Vector3 _endPosition;

            private void Awake()
            {
                _entity = GetComponent<BaseEntity>();
                _id = _entity.net.ID;
                _ins._behaviors[_id] = this;
                enabled = false;
            }

            public void StartRise(GatherConfig config)
            {
                _config = config;
                _endPosition = _entity.transform.position + Vector3.up * _config.Distance;
                enabled = true;
            }

            private void FixedUpdate()
            {
                if (_timeTaken > _config.Duration)
                {
                    OreResourceEntity ore = _entity as OreResourceEntity;
                    if (ore) 
                    {
                        ore.CleanupBonus();
                        ore._hotSpot = ore.SpawnBonusSpot(Vector3.zero);
                    }
                    
                    enabled = false;
                    _isCompleted = true;
                    _entity.transform.position = _endPosition;
                    _entity.SendNetworkUpdate();
                    DoDestroy();
                    return;
                }

                _entity.transform.position += Vector3.up * (_config.Distance * (Time.deltaTime / _config.Duration));
                _entity.SendNetworkUpdate();
                _timeTaken += Time.deltaTime;
            }

            public void DoDestroy()
            {
                Destroy(this);
            }

            private void OnDestroy()
            {
                if (!_isCompleted)
                {
                    if (_entity)
                    {
                        _entity.transform.position = _endPosition;
                        _entity.SendNetworkUpdate();
                    }

                    _isCompleted = true;
                }
                
                _ins?._behaviors.Remove(_id);
            }
        }

        private class PluginConfig
        {
            [DefaultValue("sg")]
            [JsonProperty(PropertyName = "Survey Gather Chat Command")]
            public string ChatCommand { get; set; }
            
            [DefaultValue(false)]
            [JsonProperty(PropertyName = "Allow survey charge researching")]
            public bool AllowResearching { get; set; }
            
            [DefaultValue(false)]
            [JsonProperty(PropertyName = "Allow survey charge crafting")]
            public bool AllowCrafting { get; set; }
            
            [DefaultValue(true)]
            [JsonProperty(PropertyName = "Enabled notifications")]
            public bool EnableNotifications { get; set; }
            
            [JsonProperty(PropertyName = "Survey Charge Usage Cooldowns")]
            public Hash<string, float> Cooldowns { get; set; }
            
            [JsonProperty(PropertyName = "Gather Items")]
            public List<GatherConfig> GatherItems { get; set; }
        }

        private class GatherConfig
        {
            [JsonProperty(PropertyName = "Prefab to spawn")]
            public string Prefab { get; set; }
            
            [JsonProperty(PropertyName = "Chance to spawn")]
            public float Chance { get; set; }
            
            [JsonProperty(PropertyName = "Distance to spawn underground")]
            public float Distance { get; set; }
            
            [JsonProperty(PropertyName = "Min health Percentage")]
            public float MinHealth { get; set; }
            
            [JsonProperty(PropertyName = "Max health Percentage")]
            public float MaxHealth { get; set; }
            
            [JsonProperty(PropertyName = "Rise duration (Seconds)")]
            public float Duration { get; set; }

            [JsonProperty(PropertyName = "Save Entity (Persists Across Restarts)")]
            public bool SaveEntity { get; set; }

            [JsonProperty(PropertyName = "Kill Entity In (Seconds)")]
            public float KillEntityDuration { get; set; } = 60f * 60f;
        }
        
        private class LangKeys
        {
            public const string NoPermission = "NoPermission";
            public const string Chat = "Chat";
            public const string Notification = "Notification";
            public const string HelpText = "HelpText";
            public const string Add = "Add";
            public const string NoEntity = "NoEntity";
            public const string Cooldown = nameof(Cooldown);
        }
        #endregion
    }
}
