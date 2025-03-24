using System.Collections.Generic;

using UnityEngine;

using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Neutral NPCs", "0x89A", "2.0.1")]
    [Description("NPCs only attack if they are attacked first")]
    class NeutralNPCs : RustPlugin
    {
        private const string _usePerm = "neutralnpcs.use";

        private void Init()
        {
            permission.RegisterPermission(_usePerm, this);

            if (_config.onlyAnimals)
            {
                Unsubscribe(nameof(CanBradleyApcTarget));
                Unsubscribe(nameof(CanHelicopterTarget));
            }

            if (_config.onlySelected)
            {
                if (!_config.selected.Contains("bradleyapc"))
                {
                    Unsubscribe(nameof(CanBradleyApcTarget));
                }

                if (!_config.selected.Contains("patrolhelicopter"))
                {
                    Unsubscribe(nameof(CanHelicopterTarget));
                }
            }
        }

        private object OnNpcTarget(BaseCombatEntity entity, BasePlayer target)
        {
            if (_config.onlyAnimals)
            {
                return null;
            }
            
            return CanTarget(entity, target) ? null : (object)true;
        }

        private object OnNpcTarget(BaseAnimalNPC animal, BasePlayer target)
        {
            return CanTarget(animal, target) ? null : (object)true;
        }

        private bool CanBradleyApcTarget(BradleyAPC entity, BasePlayer target)
        {
            return CanTarget(entity, target);
        }

        private bool CanHelicopterTarget(PatrolHelicopterAI entity, BasePlayer target)
        {
            return CanTarget(entity.helicopterBase, target);
        }

        #region -Helpers-

        private bool CanTarget(BaseCombatEntity entity, BasePlayer target)
        {
            if (target.IsNpc)
            {
                return true;
            }

            if (!HasPermission(target))
            {
                return true;
            }
            
            if (_config.onlySelected && !_config.selected.Contains(entity.ShortPrefabName))
            {
                return false;
            }

            return entity.lastAttacker == target && !HasForgotten(entity.lastAttackedTime);
        }

        private bool HasForgotten(float lastAttackedTime) => Time.time - lastAttackedTime > _config.forgetTime;
        private bool HasPermission(BasePlayer player) => !_config.usePermission || permission.UserHasPermission(player.UserIDString, _usePerm);

        #endregion -Helpers-

        #region -Configuration-

        private Configuration _config;
        
        private class Configuration
        {
            [JsonProperty("Use Permission")]
            public bool usePermission = false;
            
            [JsonProperty(PropertyName = "Forget time")]
            public float forgetTime = 30f;

            [JsonProperty(PropertyName = "Only animals")]
            public bool onlyAnimals = true;

            [JsonProperty(PropertyName = "Affect only selected")]
            public bool onlySelected = false;

            [JsonProperty(PropertyName = "Selected entities")]
            public List<string> selected = new List<string>();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new System.Exception();
                SaveConfig();
            }
            catch
            {
                PrintWarning("Error loading config, using default values");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => _config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion
    }
}
