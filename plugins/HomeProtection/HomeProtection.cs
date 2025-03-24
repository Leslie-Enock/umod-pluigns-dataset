namespace Oxide.Plugins
{
    [Info("Home Protection", "Wulf/lukespragg", "1.0.0")]
    [Description("Protects you and your home from intruders.")]

    class HomeProtection : RustPlugin
    {
        private void OnEntityTakeDamage(BasePlayer victim, HitInfo hitInfo)
        {
            if (hitInfo == null || victim == null || victim.userID.IsSteamId() == false)
                return;

            BasePlayer attacker = hitInfo.InitiatorPlayer;
            if (attacker == null || attacker.userID.IsSteamId() == false)
                return;

            if (attacker == victim)
                return;

            if (victim.Team != null && victim.Team.members.Contains(attacker.userID))
                return;

            if (victim.IsBuildingAuthed() && !attacker.IsBuildingAuthed())
                hitInfo.damageTypes.Clear();
        }

        private void OnEntityTakeDamage(BuildingBlock buildingBlock, HitInfo hitInfo)
        {
            if (hitInfo == null)
                return;

            BasePlayer attacker = hitInfo.InitiatorPlayer;
            if (attacker == null || attacker.userID.IsSteamId() == false)
                return;

            if (buildingBlock.OwnerID == attacker.userID)
                return;

            if (buildingBlock.GetBuildingPrivilege() != null && !attacker.IsBuildingAuthed())
                hitInfo.damageTypes.Clear();
        }
    }
}