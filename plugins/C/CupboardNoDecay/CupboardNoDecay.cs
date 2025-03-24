using System;
using Rust;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using Facepunch;

namespace Oxide.Plugins
{
    [Info("CupboardNoDecay", "Rick", "1.3.0")]
    [Description("Use cupboard to protect from decay without consuming resources.")]
    public class CupboardNoDecay : RustPlugin
    {
        private ConfigData configData;

        void OnServerInitialized()
        {
            LoadVariables();
        }

        #region config
        private class ConfigData
        {
            public bool CheckAuth { get; set; }
            public bool DecayTwig { get; set; }
            public bool DecayWood { get; set; }
            public bool DecayStone { get; set; }
            public bool DecayMetal { get; set; }
            public bool DecayArmor { get; set; }
            public float TwigRate { get; set; }
            public float WoodRate { get; set; }
            public float StoneRate { get; set; }
            public float MetalRate { get; set; }
            public float ArmorRate { get; set; }
            public float EntityRadius { get; set; }
            public string[] NeverDecay { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            string[] never = { "RHIB", "Rowboat", "HotAirBalloon", "minicopter.entity" };

            SaveConfig(new ConfigData
            {
                CheckAuth = false,
                DecayTwig = true,
                DecayWood = false,
                DecayStone = false,
                DecayMetal = false,
                DecayArmor = false,
                TwigRate = 1.0f,
                WoodRate = 0.0f,
                StoneRate = 0.0f,
                MetalRate = 0.0f,
                ArmorRate = 0.0f,
                EntityRadius = 30f,
                NeverDecay = never
            });
        }

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }

        void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region Main
        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            string entity_name = null;
            if (!info.damageTypes.Has(DamageType.Decay))
            {
                return null;
            }

            try
            {
                entity_name = entity.LookupPrefab().name;
            }
            catch
            {
                return null;
            }

            string pos = entity.transform.position.ToString();
            ulong hitEntityOwnerID = entity.OwnerID != 0 ? entity.OwnerID : info.HitEntity.OwnerID;
            float before = info.damageTypes.Get(Rust.DamageType.Decay);
            float multiplier = 1.0f;

            // First, we check for protected entities (NeverDecay)
            if (configData.NeverDecay.Contains(entity_name))
            {
                multiplier = 0.0f;

                // Apply our damage rules and return
                info.damageTypes.Scale(Rust.DamageType.Decay, multiplier);
                return null;
            }

            // Second, we check for attached (BLOCK) or nearby (ENTITY) cupboard
            BuildingBlock block = entity as BuildingBlock;

            string isblock = "";
            string buildGrade = "";
            bool hascup = false;
            if (block != null)
            {
                isblock = " (building block)";
                hascup = CheckCupboardBlock(block, info, entity_name);

                if (hascup)
                {
                    multiplier = 0.0f;
                    switch (block.grade)
                    {
                        case BuildingGrade.Enum.Twigs:
                            if (configData.DecayTwig == true)
                            {
                                multiplier = configData.TwigRate;
                            }
                            break;
                        case BuildingGrade.Enum.Wood:
                            if (configData.DecayWood == true)
                            {
                                multiplier = configData.WoodRate;
                            }
                            break;
                        case BuildingGrade.Enum.Stone:
                            if (configData.DecayStone == true)
                            {
                                multiplier = configData.StoneRate;
                            }
                            break;
                        case BuildingGrade.Enum.Metal:
                            if (configData.DecayMetal == true)
                            {
                                multiplier = configData.MetalRate;
                            }
                            break;
                        case BuildingGrade.Enum.TopTier:
                            if (configData.DecayArmor == true)
                            {
                                multiplier = configData.ArmorRate;
                            }
                            break;
                    }

                }
                else
                {

                    return null; // Standard damage rates apply
                }
            }
            else if (CheckCupboardEntity(entity, info, entity_name))
            {
                // Unprotected Entity with cupboard
                multiplier = 0.0f;

            }
            else
            {
                // Unprotected Entity with NO Cupboard

                return null; // Standard damage rates apply
            }

            // Apply our damage rules and return
            info.damageTypes.Scale(Rust.DamageType.Decay, multiplier);

            return null;
        }

        // Check that an entity is in range of a cupboard
        private bool CheckCupboardEntity(BaseEntity entity, HitInfo hitInfo, string name)
        {
            int targetLayer = LayerMask.GetMask("Construction", "Construction Trigger", "Trigger", "Deployed");
            Collider[] hit = Physics.OverlapSphere(entity.transform.position, configData.EntityRadius, targetLayer);

            // loop through hit layers and check for 'Building Privlidge'
            foreach (var ent in hit)
            {
                BuildingPrivlidge privs = ent.GetComponentInParent<BuildingPrivlidge>();
                if (privs != null)
                {
                    // cupboard overlap.  Entity safe from decay

                    if (configData.CheckAuth == true)
                    {

                        ulong hitEntityOwnerID = entity.OwnerID != 0 ? entity.OwnerID : hitInfo.HitEntity.OwnerID;
                        return CupboardAuthCheck(privs, hitEntityOwnerID);
                    }
                    else
                    {
                        return true;
                    }
                }
            }

            if (hit.Length > 0)
            {

                return false;
            }
            else
            {

            }
            return true;
        }

        // Check that a building block is owned by/attached to a cupboard
        private bool CheckCupboardBlock(BuildingBlock block, HitInfo hitInfo, string ename = "unknown")
        {
            BuildingManager.Building building = block.GetBuilding();

            if (building != null)
            {
                //if(building.buildingPrivileges == null)
                if (building.GetDominatingBuildingPrivilege() == null)
                {

                    return false;
                }

                // cupboard overlap.  Block safe from decay.  Check auth?
                if (configData.CheckAuth == true)
                {
                    ulong hitEntityOwnerID = block.OwnerID != 0 ? block.OwnerID : hitInfo.HitEntity.OwnerID;
                    foreach (var privs in building.buildingPrivileges)
                    {
                        if (CupboardAuthCheck(privs, hitEntityOwnerID) == true)
                        {
                            return true;
                        }
                    }
                }
                else
                {

                    return true;
                }
            }
            else
            {

            }
            return false;
        }

        private bool CupboardAuthCheck(BuildingPrivlidge priv, ulong hitEntityOwnerID)
        {
            string hitId = null;
            string entowner = null;
            foreach (var auth in priv.authorizedPlayers.Select(x => x.userid).ToArray())
            {

                if (auth == hitEntityOwnerID)
                {

                    return true;
                }
            }

            return false;
        }
        #endregion
    }
}

