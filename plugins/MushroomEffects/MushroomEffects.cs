using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("Mushroom Effects", "supreme", "1.1.2")]
    [Description("Make mushroom eating fun and add effects")]
    public class MushroomEffects : RustPlugin
    {
        #region Class Fields
        
        private PluginConfig _pluginConfig;

        private readonly Hash<string, Effect> _cachedEffects = new Hash<string, Effect>();
        private readonly Hash<ulong, List<Timer>> _cachedTimers = new Hash<ulong, List<Timer>>();
        private readonly Hash<ulong, int> _cachedUsedMushrooms = new Hash<ulong, int>();
        private readonly System.Random _random = new System.Random();

        private const string UiBlur = "MushroomEffects_UiBlur";
        private const string UiEffects = "MushroomEffects_UiEffects";
        private const string HardShakeEffectPrefab = "assets/bundled/prefabs/fx/screen_land.prefab";
        private const string SoftShakeEffectPrefab = "assets/bundled/prefabs/fx/takedamage_generic.prefab";
        private const string VomitEffetPrefab = "assets/bundled/prefabs/fx/gestures/drink_vomit.prefab";
        private const string BreatheEffectPrefab = "assets/prefabs/npc/bear/sound/breathe.prefab";
        private const string UsePermission = "mushroomeffects.use";
        private const int MushroomItemId = -1962971928;
        private string _cachedUi;

        #endregion

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(UsePermission, this);
        }

        private void OnServerInitialized()
        {
            CacheUiBlur();
        }

        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                DestroyMushroomEffects(player);
            }
        }
        
        private void OnItemUse(Item item, int amount)
        {
            if (item.info.itemid != MushroomItemId)
            {
                return;
            }
            
            BasePlayer player = item.GetOwnerPlayer();
            if (!player || !permission.UserHasPermission(player.UserIDString, UsePermission))
            {
                return;
            }

            ulong playerId = player.userID;
            _cachedUsedMushrooms[playerId]++;
            if (_cachedUsedMushrooms[playerId] < _random.Next(_pluginConfig.MinUsed, _pluginConfig.MaxUsed))
            {
                return;
            }
            
            List<Timer> cachedTimers = GetCachedTimers(playerId);
            int times = 0;
            // No point in implementing a proper system for such a simple plugin
            cachedTimers.Add(timer.Every(0.25f, () =>
            {
                if (_pluginConfig.EnableShakeEffect)
                {
                    SendEffectTo(HardShakeEffectPrefab, player);
                    SendEffectTo(SoftShakeEffectPrefab, player);
                }

                // instead of instantiating more timers
                if (times % 16 == 0 && _pluginConfig.EnableVomitEffect)
                {
                    SendEffectTo(VomitEffetPrefab, player);
                }

                if (times % 4 == 0 && _pluginConfig.EnableBreathEffect)
                {
                    SendEffectTo(BreatheEffectPrefab, player);
                }

                DisplayUiEffects(player);
                times++;
            }));

            DisplayUiBlur(player);
            _cachedUsedMushrooms[playerId] = 0;
            
            cachedTimers.Add(timer.Once(_pluginConfig.EffectsDuration, () => DestroyMushroomEffects(player)));
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo hitInfo)
        {
            DestroyMushroomEffects(player);
        }
        
        private void OnPlayerDisconnected(BasePlayer player)
        {
            DestroyMushroomEffects(player);
            _cachedUsedMushrooms.Remove(player.userID);
            _cachedTimers.Remove(player.userID);
        }

        #endregion

        #region Core Methods

        private void DisplayUiBlur(BasePlayer player)
        {
            if (!player)
            {
                return;
            }

            CuiHelper.DestroyUi(player, UiBlur);
            CuiHelper.AddUi(player, _cachedUi);
        }
        
        private void DisplayUiEffects(BasePlayer player)
        {
            if (!player)
            {
                return;
            }

            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = $"{GetRandomColor()}" }
            }, "Overlay", UiEffects);

            CuiHelper.DestroyUi(player, UiEffects);
            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Helper Methods
        
        private List<Timer> GetCachedTimers(ulong playerId)
        {
            return _cachedTimers[playerId] ?? (_cachedTimers[playerId] = new List<Timer>());
        }
        
        private void CacheUiBlur()
        {
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0.65", Material = "assets/content/ui/uibackgroundblur.mat" },
            }, "Overlay", UiBlur);

            _cachedUi = container.ToJson();
        }
        
        private void SendEffectTo(string effectPrefab, BasePlayer player)
        {
            Effect effect = _cachedEffects[effectPrefab];
            if (effect == null)
            {
                effect = new Effect(effectPrefab, Vector3.zero, Vector3.zero)
                {
                    attached = true
                };
                
                _cachedEffects[effectPrefab] = effect;
            }
            
            effect.entity = player.net.ID;
            EffectNetwork.Send(effect, player.net.connection);
        }

        private void DestroyMushroomEffects(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UiEffects);
            CuiHelper.DestroyUi(player, UiBlur);
            DestroyTimers(player.userID);
        }

        private void DestroyTimers(ulong playerId)
        {
            List<Timer> timers = _cachedTimers[playerId];
            if (timers == null)
            {
                return;
            }
            
            foreach (Timer cachedTimer in timers)
            {
                cachedTimer?.Destroy();
            }
        }
        
        private string GetRandomColor()
        {
            return $"{_random.NextDouble()} {_random.NextDouble()} {_random.NextDouble()} {_pluginConfig.ColorOpacity}";
        }

        #endregion

        #region Configuration

        private class PluginConfig
        {
            [JsonProperty(PropertyName = "Duration of the effects")]
            public float EffectsDuration { get; set; } = 10f;
            
            [JsonProperty(PropertyName = "Minimum Amount Of Mushrooms required to trigger the effect")]
            public int MinUsed { get; set; } = 1;

            [JsonProperty(PropertyName = "Maximum Amount Of Mushrooms required to trigger the effect")]
            public int MaxUsed { get; set; } = 5;

            [JsonProperty(PropertyName = "Opacity of the colors")]
            public float ColorOpacity { get; set; } = 0.3f;

            [JsonProperty(PropertyName = "Enable vomit effect")] 
            public bool EnableVomitEffect { get; set; } = true;

            [JsonProperty(PropertyName = "Enable breath effect")]
            public bool EnableBreathEffect { get; set; } = true;

            [JsonProperty(PropertyName = "Enable shake effect")]
            public bool EnableShakeEffect { get; set; } = true;

        }
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _pluginConfig = Config.ReadObject<PluginConfig>();
                if (_pluginConfig == null)
                {
                    throw new Exception();
                }
                
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_pluginConfig);

        protected override void LoadDefaultConfig() => _pluginConfig = new PluginConfig();

        #endregion
    }
}