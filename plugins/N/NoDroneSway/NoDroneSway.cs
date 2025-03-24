using Network;

namespace Oxide.Plugins
{
    [Info("No Drone Sway", "WhiteThunder", "1.0.4")]
    [Description("Drones no longer sway in the wind, if they have attachments.")]
    internal class NoDroneSway : CovalencePlugin
    {
        #region Fields

        private const int DroneThrottleUpFlag = (int)Drone.Flag_ThrottleUp;
        private const int DroneFlyingFlag = (int)Drone.Flag_Flying;
        private readonly object False = false;

        #endregion

        #region Hooks

        private void OnEntitySaved(Drone drone, BaseNetworkable.SaveInfo saveInfo)
        {
            if ((saveInfo.msg.baseEntity.flags & DroneFlyingFlag) == 0
                || !drone.ControllingViewerId.HasValue
                || ShouldSway(drone))
                return;

            // Don't change flags for the remote controller because that would prevent viewing the pitch.
            // This approach is possible because network caching is disabled for RemoteControlEntity.
            var controllerSteamId = drone.ControllingViewerId.Value.SteamId;
            if (controllerSteamId != 0
                && controllerSteamId == (saveInfo.forConnection?.player as BasePlayer)?.userID
                && IsControllingDrone(saveInfo.forConnection, drone))
                return;

            saveInfo.msg.baseEntity.flags = ModifyDroneFlags(drone);
        }

        private object OnEntityFlagsNetworkUpdate(Drone drone)
        {
            if (((int)drone.flags & DroneFlyingFlag) == 0
                || !drone.ControllingViewerId.HasValue
                || ShouldSway(drone))
                return null;

            var subscribers = drone.GetSubscribers();
            if (subscribers is { Count: > 0 })
            {
                var controllerSteamId = drone.ControllingViewerId?.SteamId ?? 0;
                if (controllerSteamId == 0)
                {
                    // No player is controlling the drone, so send the same update to all subscribers.
                    SendFlagsUpdate(drone, ModifyDroneFlags(drone), new SendInfo(subscribers));
                }
                else
                {
                    // A player is controlling the drone, so we might need to send that player different flags.
                    var otherConnections = Facepunch.Pool.GetList<Connection>();

                    Connection controllerConnection = null;
                    foreach (var connection in subscribers)
                    {
                        if (connection.ownerid == controllerSteamId)
                        {
                            controllerConnection = connection;
                        }
                        else
                        {
                            otherConnections.Add(connection);
                        }
                    }

                    if (controllerConnection != null && !IsControllingDrone(controllerConnection, drone))
                    {
                        // The controller isn't using a computer station (e.g., RidableDrones plugin),
                        // so send them the same snapshot as other players.
                        otherConnections.Add(controllerConnection);
                        controllerConnection = null;
                    }

                    var flags = ModifyDroneFlags(drone);

                    if (otherConnections.Count > 0)
                    {
                        SendFlagsUpdate(drone, flags, new SendInfo(otherConnections));
                    }

                    if (controllerConnection != null)
                    {
                        SendFlagsUpdate(drone, (int)drone.flags, new SendInfo(controllerConnection));
                    }

                    Facepunch.Pool.FreeList(ref otherConnections);
                }
            }

            drone.gameObject.SendOnSendNetworkUpdate(drone);
            return False;
        }

        #endregion

        #region Helpers

        private static class RCUtils
        {
            public static T GetControlledEntity<T>(BasePlayer player) where T : class
            {
                var station = player.GetMounted() as ComputerStation;
                if ((object)station == null)
                    return null;

                return station.currentlyControllingEnt.Get(serverside: true) as T;
            }
        }

        private static bool IsControllingDrone(Connection connection, Drone drone)
        {
            var player = connection.player as BasePlayer;
            if ((object)player == null)
                return false;

            return RCUtils.GetControlledEntity<Drone>(player) == drone;
        }

        private static void SendFlagsUpdate(BaseEntity entity, int flags, SendInfo sendInfo)
        {
            var write = Net.sv.StartWrite();
            write.PacketID(Message.Type.EntityFlags);
            write.EntityID(entity.net.ID);
            write.Int32(flags);
            write.Send(sendInfo);
        }

        private static int ModifyDroneFlags(BaseEntity drone)
        {
            var flags = (int)drone.flags;

            if ((flags & DroneFlyingFlag) != 0)
            {
                flags = flags & ~DroneFlyingFlag | DroneThrottleUpFlag;
            }

            return flags;
        }

        private static bool ShouldSway(Drone drone)
        {
            // Drones with attachments should not sway.
            if (drone.children.Count > 0)
            {
                for (var i = 0; i < drone.children.Count; i++)
                {
                    var sphereChild = drone.children[i] as SphereEntity;
                    if ((object)sphereChild == null)
                        return false;

                    for (var j = 0; j < sphereChild.children.Count; j++)
                    {
                        var grandChild = sphereChild.children[j];

                        // Resized search lights are permitted (Drone Lights).
                        if (grandChild is SearchLight)
                            continue;

                        return false;
                    }
                }
            }

            // Drones with a re-parented rigid body are probably resized and should not sway.
            if (drone != null && drone.body.gameObject != drone.gameObject)
                return false;

            return true;
        }

        #endregion
    }
}
