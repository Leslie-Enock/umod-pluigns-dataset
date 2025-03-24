using System;
using System.Collections.Generic;
using UnityEngine;
using Facepunch;

namespace Oxide.Plugins
{
    [Info("Anti-Heli Hoarding", "Thisha", "0.2.0")]
    public class AntiHeliHoarding : RustPlugin
    {
        #region ChatCommands
        [ChatCommand("helidecay")]
        void ShowDecay(BasePlayer player)
        {
            if (player == null)
                return;

            if (!player.IsAdmin)
                return;

            MiniCopter heli = GetLookingAtMiniCopter(player);
            if (heli == null)
                return;

            player.ChatMessage(MinicoptersInRange(heli.ServerPosition).ToString());
        }
        #endregion ChatCommands

        #region Hooks
        private void Init()
        {
            try
            {
                LoadConfigData();
            }
            catch
            {
                LoadDefaultConfig();
                LoadConfigData();
            }
        }

        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (entity == null || hitInfo == null)
                return;

            if (!(entity is MiniCopter))
                return;

            if (!hitInfo.damageTypes.Has(Rust.DamageType.Decay)) return;

            MiniCopter heli = entity as MiniCopter;

            int noOfHelis = MinicoptersInRange(heli.ServerPosition);
            if (noOfHelis >= config.MinimumQuantity)
                hitInfo.damageTypes.Scale(Rust.DamageType.Decay, noOfHelis);
            else
                hitInfo.damageTypes.Scale(Rust.DamageType.Decay, 1);
            return;
        }
        #endregion Hooks

        #region Config
        private ConfigData config = new ConfigData();
        class ConfigData
        {
            public int DetectionRadius;
            public int MinimumQuantity;
        }

        private object ConfigValue(string value)
        {
            switch (value)
            {
                case "Detection Radius":
                    if (Config[value] == null)
                        return 30;
                    else
                        return Config[value];
                case "Minimum Quantity":
                    if (Config[value] == null)
                        return 2;
                    else
                        return Config[value];
                default:
                    return null;
            }
        }

        protected override void LoadDefaultConfig()
        {
            Config["Detection Radius"] = ConfigValue("Detection Radius");
            Config["Minimum Quantity"] = ConfigValue("Minimum Quantity");

            SaveConfig();
        }

        private void LoadConfigData()
        {
            config.DetectionRadius = (int)Config["Detection Radius"];
            config.MinimumQuantity = (int)Config["Minimum Quantity"];
        }
        #endregion Config

        #region helpers
        private MiniCopter GetLookingAtMiniCopter(BasePlayer player)
        {
            RaycastHit raycastHit;
            if (Physics.Raycast(player.eyes.position, player.eyes.HeadForward(), out raycastHit, 5f, 1218652417, QueryTriggerInteraction.Ignore))
            {
                BaseEntity entity = raycastHit.GetEntity();
                if (entity)
                {
                    return entity.GetComponent<MiniCopter>();
                }
            }
            return null;
        }

        private int MinicoptersInRange(Vector3 pos)
        {
            int copters = 0;
            
            List<MiniCopter> list = Pool.GetList<MiniCopter>();
            Vis.Entities<MiniCopter>(pos, config.DetectionRadius, list, -13, QueryTriggerInteraction.Collide);
            foreach (MiniCopter copter in list)
            {
                if (!copter.IsDestroyed)
                {
                    copters = copters + 1;
                }
            }
            Pool.FreeList<MiniCopter>(ref list);

            decimal factor = copters / 8;
            copters = (int)Math.Ceiling(factor);

            if (copters < 1)
                copters = 1;

            return copters;
        }
        
        #endregion helpers
    }
}