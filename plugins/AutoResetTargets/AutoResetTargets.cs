using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Auto Reset Targets", "Default", "1.0.5")]
    [Description("Automatically resets knocked down targets after a set amount of time")]
    public class AutoResetTargets : RustPlugin
    {

        public float activeTime;
        bool Changed = false;
        private const string permissionName = "autoresettargets.use";
        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            ReactiveTarget target = entity as ReactiveTarget;

            var p = entity.OwnerID.ToString();

            if (target != null && target.IsKnockedDown() && permission.UserHasPermission(p, permissionName))
            {
                timer.Once(activeTime, () =>
                {
                    target.ResetTarget();
                });
            }
            
        }


        object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }

        void LoadVariables()
        {

            activeTime = Convert.ToSingle(GetConfig("ART", "Time until reset", 1.0f));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        void Init()
        {
            LoadVariables();
            permission.RegisterPermission(permissionName, this);
        }


    }
}
