namespace Oxide.Plugins
{
  [Info("Safe Zone Harvest Block", "gnif", "1.0.2")]
  [Description("Prevents harvesting in the Safe Zones")]
  internal class SafeZoneHarvestBlock : CovalencePlugin
  {
    private const string BypassPerm = "safezoneharvestblock.bypass";

    private void Init()
    {
      permission.RegisterPermission(BypassPerm, this);
      timer.Every(1, () => { OnTick(); });
    }

    private void OnTick()
    {
      foreach (var player in BasePlayer.activePlayerList)
      {
        if (player == null || player.IsNpc)
          continue;

        if (permission.UserHasPermission(player.UserIDString, BypassPerm))
          continue;

        if (!player.InSafeZone())
        {
          if (player.inventory.containerBelt.capacity == 0)
          {
            player.inventory.containerBelt.capacity = 6;
            player.SendNetworkUpdateImmediate();
          }
          continue;
        }

        bool update = false;
        if (player.inventory.containerBelt.capacity > 0)
        {
          player.inventory.containerBelt.capacity = 0;
          update = true;
        }


        Item activeItem = player.GetActiveItem();
        if (activeItem != null)
        {
          HeldEntity heldEntity = activeItem.GetHeldEntity() as HeldEntity;
          if (heldEntity)
          {
            heldEntity.SetHeld(false);
            update = true;
          }
        }

        if (update)
          player.SendNetworkUpdateImmediate();
      }
    }
  }
}
