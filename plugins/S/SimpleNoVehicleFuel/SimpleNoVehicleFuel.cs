using Oxide.Core.Plugins;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Simple No Vehicle Fuel", "Mabel", "1.1.0")]
    [Description("Removes requirement of fuel in all vehicles")]

    public class SimpleNoVehicleFuel : RustPlugin
    {
        [PluginReference] readonly Plugin Convoy;

        #region Oxide Hooks

        private void Init()
        {
            Unsubscribe(nameof(OnEntitySpawned));
        }

        private void OnServerInitialized()
        {
            Subscribe(nameof(OnEntitySpawned));

            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity is Catapult) return;

                if (entity is BaseVehicle vehicle)
                {
                    bool isConvoyVehicle = Convoy != null && (bool)Convoy?.Call("IsConvoyVehicle", vehicle);
                  
                    if (!isConvoyVehicle)
                    {
                        ModifyVehicle(vehicle);
                    }
                }
                else if (entity is HotAirBalloon balloon)
                {
                    ModifyBalloon(balloon);
                }
            }
        }

        private void Unload()
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity is BaseVehicle vehicle)
                {
                    bool isConvoyVehicle = Convoy != null && (bool?)Convoy.Call("IsConvoyVehicle", vehicle) == true;

                    if (!isConvoyVehicle)
                    {
                        ResetVehicle(vehicle);
                    }
                }
                else if (entity is HotAirBalloon balloon)
                {
                    ResetBalloon(balloon);
                }
            }
        }

        private void OnEntitySpawned(BaseVehicle vehicle)
        {
            if (vehicle is Catapult) return;

            NextTick(() =>
            {
                if (vehicle == null || !vehicle.IsValid())
                    return;

                bool isConvoyVehicle = Convoy != null && (bool?)Convoy.Call("IsConvoyVehicle", vehicle) == true;

                if (!isConvoyVehicle)
                {
                    ModifyVehicle(vehicle);
                }
            });
        }

        private void OnEntitySpawned(HotAirBalloon balloon)
        {
            NextTick(() =>
            {
                if (balloon == null) return;

                if (balloon.IsValid())
                {
                    ModifyBalloon(balloon);
                }
            });
        }

        #endregion

        #region Core

        private void ModifyVehicle(BaseVehicle vehicle)
        {
            var fuelSystem = vehicle.GetFuelSystem() as EntityFuelSystem;
            var container = fuelSystem?.fuelStorageInstance.Get(true);
            if (container == null)
            {
                container = vehicle.GetComponentsInChildren<StorageContainer>().FirstOrDefault(x => x.inventory.onlyAllowedItems != null);
            }

            if (container == null)
            {
                return;
            }

            var item = container.inventory.GetSlot(0);
            if (item == null)
            {
                item = ItemManager.Create(container.inventory.onlyAllowedItems.FirstOrDefault());
                if (item == null)
                {
                    return;
                }
                item.MoveToContainer(container.inventory);
            }

            item.amount = 200;
            item.skin = 12345;
            item.OnDirty += item1 => Refill(item1);
            item.SetFlag(global::Item.Flag.IsLocked, true);
            container.dropsLoot = false;
            container.inventory.SetFlag(ItemContainer.Flag.IsLocked, true);
            container.SetFlag(BaseEntity.Flags.Locked, true);
        }

        private void ModifyBalloon(HotAirBalloon balloon)
        {
            var fuelSystem = balloon.GetFuelSystem() as EntityFuelSystem;
            var container = fuelSystem?.fuelStorageInstance.Get(true);
            if (container == null)
            {
                container = balloon.GetComponentsInChildren<StorageContainer>().FirstOrDefault(x => x.inventory.onlyAllowedItems != null);
            }

            if (container == null)
            {
                return;
            }

            var item = container.inventory.GetSlot(0);
            if (item == null)
            {
                item = ItemManager.Create(container.inventory.onlyAllowedItems.FirstOrDefault());
                if (item == null)
                {
                    return;
                }
                item.MoveToContainer(container.inventory);
            }

            item.amount = 200;
            item.skin = 12345;
            item.OnDirty += item1 => Refill(item1);
            item.SetFlag(global::Item.Flag.IsLocked, true);
            container.dropsLoot = false;
            container.inventory.SetFlag(ItemContainer.Flag.IsLocked, true);
            container.SetFlag(BaseEntity.Flags.Locked, true);
        }

        private void ResetVehicle(BaseVehicle vehicle)
        {
            var fuelSystem = vehicle.GetFuelSystem() as EntityFuelSystem;
            var container = fuelSystem?.fuelStorageInstance.Get(true);
            if (container == null)
            {
                container = vehicle.GetComponentsInChildren<StorageContainer>().FirstOrDefault(x => x.inventory.onlyAllowedItems != null);
            }

            if (container == null)
            {
                return;
            }

            var item = container.inventory.GetSlot(0);
            if (item != null && item.skin == 12345)
            {
                item.DoRemove();
            }

            container.inventory.SetFlag(ItemContainer.Flag.IsLocked, false);
            container.SetFlag(BaseEntity.Flags.Locked, false);
        }

        private void ResetBalloon(HotAirBalloon balloon)
        {
            var fuelSystem = balloon.GetFuelSystem() as EntityFuelSystem;
            var container = fuelSystem?.fuelStorageInstance.Get(true);
            if (container == null)
            {
                container = balloon.GetComponentsInChildren<StorageContainer>().FirstOrDefault(x => x.inventory.onlyAllowedItems != null);
            }

            if (container == null)
            {
                return;
            }

            var item = container.inventory.GetSlot(0);
            if (item != null && item.skin == 12345)
            {
                item.DoRemove();
            }

            container.inventory.SetFlag(ItemContainer.Flag.IsLocked, false);
            container.SetFlag(BaseEntity.Flags.Locked, false);
        }

        private void Refill(Item item)
        {
            item.amount = 200;
        }

        #endregion
    }
}