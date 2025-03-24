using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Enderpearl", "Wolfleader101", "0.4.1")]
    [Description("Throw an ender pearl and teleport to its location")]
    class Enderpearl : RustPlugin
    {
        #region Variables

        private PluginConfig config;
        public const string enderPearlPerms = "enderpearl.use";

        #endregion

        #region Hooks

        private void Init()
        {
            config = Config.ReadObject<PluginConfig>();

            permission.RegisterPermission(enderPearlPerms, this);
        }

        void OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            if (info == null) return;
            if (!permission.UserHasPermission(attacker.UserIDString, enderPearlPerms)) return;
            if (!info.IsProjectile()) return;
            
            //var weapon = info.Weapon.GetItem();  // ONLY WORKS FOR GUNS
            //Puts(info.WeaponPrefab.name);

            string entName = info.WeaponPrefab.name;
            if (entName != config.enderpearl) return;

            Teleport(attacker, info);
        }

        #endregion

        #region Custom Methods

        void Teleport(BasePlayer attacker, HitInfo info)
        {
            Vector3 entLoc = info.HitPositionWorld;
            attacker.transform.position = entLoc;
            info.ProjectilePrefab.conditionLoss += 1f;
        }

        #endregion

        #region Config

        private class PluginConfig
        {
            [JsonProperty("Enderpearl")] public string enderpearl { get; set; }
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                enderpearl = "snowball.entity"
            };
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true);
        }

        #endregion
    }
}
