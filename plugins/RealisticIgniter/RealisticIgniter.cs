using Rust;
using ConVar;
using Network;
using Oxide.Core;
using System;
using UnityEngine;
using UnityEngine.Assertions;

namespace Oxide.Plugins
{
    [Info("Realistic Igniter", "ArtiIOMI", "1.1.4")]
    [Description("You can now set fire to the fireplace using the flamethrower or torch.")]
    internal class RealisticIgniter : RustPlugin
    {
        void OnEntityTakeDamage(BaseOven entity, HitInfo info)
        {
            if(entity == null || info == null)
                return;

            if(!entity.ShortPrefabName.Contains("campfire"))
                return;

            if(entity.IsOn())
                return;

            if(entity == null)
                return;

            if(!entity.inventory.itemList.Exists(x => x.info.shortname == "wood"))
                return;

            if(!info.damageTypes.Has(DamageType.Heat) && !info.damageTypes.Has(DamageType.Blunt))
                return;

            if(info.damageTypes.Has(DamageType.Heat)){
                entity.StartCooking();
                info.damageTypes = new DamageTypeList();
            }else{
                if(info.WeaponPrefab == null)
                    return;
                if((info.WeaponPrefab.ShortPrefabName.Contains("torch.entity") && info.WeaponPrefab.IsOn())){
                    entity.StartCooking();
                }
                info.damageTypes = new DamageTypeList();
            }
        }
    }
}
