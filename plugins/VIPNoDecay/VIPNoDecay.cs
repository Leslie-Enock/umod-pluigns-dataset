using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("VIPNoDecay", "ColonBlow", "1.0.4")]
    [Description("Disables Decay Damage for player or oxide group with VIP permissions")]

    class VIPNoDecay : CovalencePlugin
    {
        const string permVIP = "vipnodecay.vip";

        private void OnServerInitialized()
        {
            permission.RegisterPermission(permVIP, this);
        }

        bool HasPermission(ulong playerID, string perm) => permission.UserHasPermission(playerID.ToString(), perm);

        private object OnEntityTakeDamage(DecayEntity decayEntity, HitInfo hitInfo)
        {
            if (hitInfo == null || !hitInfo.damageTypes.GetMajorityDamageType().ToString().Contains("Decay")) return null;
            var ownerid = decayEntity.OwnerID;
            if (ownerid != null && HasPermission(ownerid, "vipnodecay.vip"))
            {
                decayEntity.DecayTouch();
                return true;
            }
            return null;
        }
    }
}