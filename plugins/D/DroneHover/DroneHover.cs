using Newtonsoft.Json;
using Oxide.Core;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Drone Hover", "WhiteThunder", "1.0.5")]
    [Description("Allows RC drones to hover in place when a player disconnects control at a computer station.")]
    internal class DroneHover : CovalencePlugin
    {
        #region Fields

        private const string PermissionUse = "dronehover.use";

        private readonly object False = false;

        private StoredData _pluginData;

        #endregion

        #region Hooks

        private void Init()
        {
            _pluginData = StoredData.Load();

            permission.RegisterPermission(PermissionUse, this);
        }

        private void Unload()
        {
            OnServerSave();
        }

        private void OnServerInitialized()
        {
            if (_pluginData.HoveringDrones == null)
                return;

            foreach (var entity in RemoteControlEntity.allControllables)
            {
                var drone = entity as Drone;
                if (drone != null && _pluginData.HoveringDrones.Contains(drone.net.ID.Value))
                {
                    MaybeStartDroneHover(drone, null);
                }
            }
        }

        private void OnServerSave()
        {
            _pluginData.Save();
        }

        private void OnNewSave()
        {
            _pluginData = StoredData.Clear();
        }

        private void OnBookmarkControlStarted(ComputerStation station, BasePlayer player, string bookmarkName, Drone drone)
        {
            if (!RCUtils.HasFakeController(drone))
                return;

            RCUtils.RemoveController(drone);
            RCUtils.RemoveViewer(drone, player);
            RCUtils.AddViewer(drone, player);
            station.SetFlag(ComputerStation.Flag_HasFullControl, true, networkupdate: false);
            station.SendNetworkUpdateImmediate();
        }

        private void OnBookmarkControlEnded(ComputerStation station, BasePlayer player, Drone drone)
        {
            OnDroneControlEnded(drone, player);
        }

        private void OnEntityKill(Drone drone)
        {
            _pluginData.HoveringDrones.Remove(drone.net.ID.Value);
        }

        // This hook is exposed by plugin: Remover Tool (RemoverTool).
        private object canRemove(BasePlayer player, Drone drone)
        {
            // Null check since somebody reported this method was somehow throwing NREs.
            if (drone == null)
                return null;

            if (drone.IsBeingControlled)
                return False;

            return null;
        }

        // This hook is exposed by plugin: Ridable Drones (RidableDrones).
        private void OnDroneControlEnded(Drone drone, BasePlayer player)
        {
            if (drone == null)
                return;

            MaybeStartDroneHover(drone, player);
        }

        #endregion

        #region Helpers

        private static class RCUtils
        {
            public static bool HasFakeController(IRemoteControllable controllable)
            {
                return controllable.ControllingViewerId?.SteamId == 0;
            }

            public static void RemoveController(IRemoteControllable controllable)
            {
                var controllerId = controllable.ControllingViewerId;
                if (controllerId.HasValue)
                {
                    controllable.StopControl(controllerId.Value);
                }
            }

            public static bool AddViewer(IRemoteControllable controllable, BasePlayer player)
            {
                return controllable.InitializeControl(new CameraViewerId(player.userID, 0));
            }

            public static void RemoveViewer(IRemoteControllable controllable, BasePlayer player)
            {
                controllable.StopControl(new CameraViewerId(player.userID, 0));
            }

            public static bool AddFakeViewer(IRemoteControllable controllable)
            {
                return controllable.InitializeControl(new CameraViewerId());
            }
        }

        private bool HoverWasBlocked(Drone drone, BasePlayer formerPilot)
        {
            var hookResult = Interface.CallHook("OnDroneHoverStart", drone, formerPilot);
            return hookResult is bool && (bool)hookResult == false;
        }

        private bool ShouldHover(Drone drone, BasePlayer formerPilot)
        {
            if (drone.IsBeingControlled || drone.isGrounded)
                return false;

            if (formerPilot != null && !permission.UserHasPermission(formerPilot.UserIDString, PermissionUse))
                return false;

            if (HoverWasBlocked(drone, formerPilot))
                return false;

            return true;
        }

        private void MaybeStartDroneHover(Drone drone, BasePlayer formerPilot)
        {
            if (!ShouldHover(drone, formerPilot))
            {
                _pluginData.HoveringDrones.Remove(drone.net.ID.Value);
                return;
            }

            RCUtils.AddFakeViewer(drone);
            drone.currentInput.Reset();
            _pluginData.HoveringDrones.Add(drone.net.ID.Value);
            Interface.CallHook("OnDroneHoverStarted", drone, formerPilot);
        }

        #endregion

        #region Data

        private class StoredData
        {
            [JsonProperty("HoveringDrones")]
            public HashSet<ulong> HoveringDrones = new HashSet<ulong>();

            public static StoredData Load() =>
                Interface.Oxide.DataFileSystem.ReadObject<StoredData>(nameof(DroneHover)) ?? new StoredData();

            public static StoredData Clear() => new StoredData().Save();

            public StoredData Save()
            {
                Interface.Oxide.DataFileSystem.WriteObject(nameof(DroneHover), this);
                return this;
            }
        }

        #endregion
    }
}
