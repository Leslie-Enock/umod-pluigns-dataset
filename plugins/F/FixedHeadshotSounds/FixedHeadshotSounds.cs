using UnityEngine;

namespace Oxide.Plugins
{
    [Info( "Fixed Headshot Sounds", "Waggy", "1.0.1" )]
    [Description( "Fixes headshot sounds not playing for melee PvP and for animals" )]

    class FixedHeadshotSounds : RustPlugin
    {
        public static Effect headshotEffect;

        public void CreateHeadshotEffect( BasePlayer player )
        {
            headshotEffect = new Effect();
            headshotEffect.Init( Effect.Type.Generic, player, 0, Vector3.zero, Vector3.zero, null );
            headshotEffect.pooledString = "assets/bundled/prefabs/fx/headshot.prefab";
        }

        public void SendHeadshotEffect( BasePlayer player )
        {
            EffectNetwork.Send( headshotEffect, player.Connection );
        }

        public void CheckHeadshot( HitInfo info )
        {
            var player = info.InitiatorPlayer;

            if ( player != null )
            {
                if ( headshotEffect == null )
                {
                    CreateHeadshotEffect( player );
                }
                
                SendHeadshotEffect( player );
            }
        }

        object OnEntityTakeDamage( BaseCombatEntity entity, HitInfo info )
        {
            if ( entity is BaseNpc && info.isHeadshot )
            {
                CheckHeadshot( info );
            }
            else if ( entity is BasePlayer && info.isHeadshot )
            {
                switch ( info.damageTypes.GetMajorityDamageType() )
                {
                    default: return null;

                    case Rust.DamageType.Blunt:
                    case Rust.DamageType.Slash:
                    case Rust.DamageType.Stab:
                        {
                            CheckHeadshot( info );
                            return null;
                        }
                }
            }

            return null;
        }
    }
}