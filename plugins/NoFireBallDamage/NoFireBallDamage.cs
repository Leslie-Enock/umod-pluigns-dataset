using Rust;
namespace Oxide.Plugins
{
    [Info("No FireBall Damage", "Lincoln", "1.0.2")]
    [Description("Prevent fireballs from damaging things.")]

    public class NoFireBallDamage : CovalencePlugin
    {
        void OnFireBallDamage(FireBall fire, BaseCombatEntity entity, HitInfo info)
        {
            if ((entity == null) || (info.damageTypes.GetMajorityDamageType() != DamageType.Heat)) return;
            info.damageTypes.ScaleAll(0f);
        }
    }
}