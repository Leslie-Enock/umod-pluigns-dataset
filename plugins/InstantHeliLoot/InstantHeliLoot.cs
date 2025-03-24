using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Instant Heli Loot", "August", "1.0.3")]
    [Description("Makes Heli (and Bradley) Gibs lootable/harvestable immediately after being destroyed.")]
    class InstantHeliLoot : CovalencePlugin
    {
        protected override void LoadDefaultConfig()
        {
            Config["UnlockBradleyCrates"] = true;
            Config["UnlockHelicopterCrates"] = true;
            Config["UnlockHelicopterGibs"] = true;
            Config["RemoveFireballs"] = true;
        }

        private void Init()
        {
            if (!(bool) Config["UnlockBradleyCrates"] && !(bool) Config["UnlockHelicopterCrates"])
            {
                Unsubscribe( nameof( OnEntityDeath ) );
            }      
        }

        private void OnServerShutdown() => SaveConfig();
        
        private void UnlockCrates(Vector3 pos)
        {
            List<LockedByEntCrate> crates = new List<LockedByEntCrate>();
                
            Vis.Entities(pos, 20f, crates);

            foreach (var crate in crates)
            {
                crate.SetLocked(false);
            }
        }

        private void KillFire(Vector3 pos)
        {
            List<FireBall> fireBalls = new List<FireBall>();
            Vis.Entities(pos, 20f, fireBalls);
            if (fireBalls.Count > 0)
            {
                foreach ( FireBall fb in fireBalls )
                {
                    fb.Kill();
                }
            }
        }
        private void OnEntitySpawned(HelicopterDebris debris)
        {
            if ((bool) Config["UnlockHelicopterGibs"])
            {
                NextTick( () =>
                {
                    debris.tooHotUntil = -1;
                } );
            }           
        }
        private void OnEntityDeath(BradleyAPC apc, HitInfo info)
        {
            var pos = apc.transform.position;
            if ((bool) Config["UnlockBradleyCrates"])
            {
                NextTick( () => UnlockCrates(pos));
            }
            if ((bool) Config["RemoveFireballs"])
            {
                NextTick( () => { KillFire( pos ); } );
            }
        }

        private void OnEntityDeath(BaseHelicopter heli, HitInfo info)
        {
            var pos = heli.transform.position;
            if ((bool) Config["UnlockHelicopterCrates"])
            {
                NextTick( () => UnlockCrates(pos));
            }
            if ((bool) Config["RemoveFireballs"])
            {
                NextTick( () => { KillFire( pos ); } );
            }
        }
    }
}