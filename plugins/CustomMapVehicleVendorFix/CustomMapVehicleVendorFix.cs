using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Custom Map Vehicle Vendor Fix", "Ryz0r", "1.0.5")]
    [Description("Fixes Airwolf & Boat spawner on custom maps. Previously, a player could buy immediately after completing a transaction.")]
    public class CustomMapVehicleVendorFix : RustPlugin
    {
        private const string AirwolfConversationalistPrefab = "assets/prefabs/npc/bandit/shopkeepers/bandit_conversationalist.prefab";
        private const string BoatVendorPrefab = "assets/prefabs/npc/bandit/shopkeepers/boat_shopkeeper.prefab";

        private void OnServerInitialized()
        {
            var count = 0;
            var vendorEntities = BaseNetworkable.serverEntities.OfType<VehicleVendor>();
            if (!vendorEntities.Any())
            {
                Puts("There were no Vehicle Vendors found on this map. You're good to go!");
                return;
            }
            
            
            foreach (var vendor in vendorEntities)
            {
                if (vendor == null) continue;
                if (vendor.PrefabName != AirwolfConversationalistPrefab && vendor.PrefabName != BoatVendorPrefab) continue;
                if (vendor.GetVehicleSpawner() != null && vendor.vehicleSpawner != null) continue;
                
                var spawners = new List<VehicleSpawner>();
                Vis.Entities(vendor.transform.position, vendor.PrefabName == AirwolfConversationalistPrefab ? 40f : 20f, spawners);

                if (spawners.Count < 1)
                {
                    Puts($"Vehicle Vendor ({vendor.net.ID}) @ {vendor.transform.position} did not have any nearby spawners. Check your map file if possible.");
                    continue;
                }
                
                if (spawners.Count > 1)
                {
                    vendor.spawnerRef.Set(spawners.OrderBy(x => Vector3.Distance(x.transform.position, vendor.transform.position)).First());
                    Puts($"Vehicle Vendor ({vendor.net.ID}) was missing a Vehicle Spawner @ {vendor.transform.position}!");
                    count += 1;
                }
                else
                {
                    vendor.spawnerRef.Set(spawners[0]);
                    Puts($"Vehicle Vendor ({vendor.net.ID}) was missing a Vehicle Spawner @ {vendor.transform.position}!");
                    count += 1;
                }
            }
            
            Puts($"Fixed {count} broken Vehicle Vendors on your map!");
        }
    }
}