using Rust;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Realistic Torch", "Synvy", "1.1.1")]
    [Description("Prevents cold damage to players holding a lit torch.")]
    public class RealisticTorch : RustPlugin
    {
        #region Initialize

        private const string _perm = "realistictorch.use";

        private void Init()
        {
            permission.RegisterPermission(_perm, this);
        }

        #endregion Initialize

        #region Hooks

        private void OnRunPlayerMetabolism(PlayerMetabolism metabolism, BaseCombatEntity entity)
        {
            var player = entity as BasePlayer;

            if (entity is BasePlayer && entity != null)
            {
                if (HasPerm(player.UserIDString, _perm) && IsHoldingTorch(player) && IsTorchIgnited(player))
                {
                    if (player.metabolism.temperature.value < 26)
                    {
                        player.metabolism.temperature.value = 26;
                    }
                }
            }
        }

        #endregion Hooks

        #region Helpers

        private bool HasPerm(string id, string perm) => permission.UserHasPermission(id, perm);

        private bool IsHoldingTorch(BasePlayer player)
        {
            var heldItem = player.GetActiveItem()?.info.shortname ?? "null";
            return heldItem == "torch" || heldItem == "torch.torch.skull";
        }

        private bool IsTorchIgnited(BasePlayer player)
        {
            HeldEntity heldEntity = player.GetHeldEntity();

            if (heldEntity != null)
            {
                return heldEntity.HasFlag(BaseEntity.Flags.On);
            }

            return false;
        }

        #endregion Helpers
    }
}