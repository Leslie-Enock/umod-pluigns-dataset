using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Elevator Counters", "WhiteThunder", "1.0.2")]
    [Description("Allows wiring counters into elevators to display the current floor and function as a call button.")]
    internal class ElevatorCounters : CovalencePlugin
    {
        #region Fields

        private static readonly PropertyInfo ElevatorLiftOwnerProperty = typeof(ElevatorLift).GetProperty("owner", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private const float MaxCounterUpdateFrequency = 0.4f;

        private readonly Dictionary<NetworkableId, Action> liftTimerActions = new();

        #endregion

        #region Hooks

        private void OnServerInitialized()
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var counter = entity as PowerCounter;
                if (counter != null)
                {
                    HandleCounterInit(counter);
                }
            }
        }

        private void HandleCounterInit(PowerCounter counter)
        {
            // Ignore counters that have connected inputs since those should function normally
            if (HasConnectedInput(counter))
                return;

            // Ignore counters not connected to elevators
            var elevator = GetConnectedElevator(counter);
            if (elevator == null)
                return;

            var topElevator = GetTopElevator(elevator);
            if (IsPowered(topElevator))
            {
                InitializeCounter(counter, GetDisplayFloor(topElevator));
            }
        }

        // When an elevator is removed without adding a new one
        // Possibly toggle all previously attached counters based on new power state
        private void OnEntityKill(Elevator elevator)
        {
            var bottomElevator = GetBottomElevator(elevator);
            var counters = GetAllConnectedCounters(GetTopElevator(elevator));
            if (counters == null)
                return;

            NextTick(() =>
            {
                if (bottomElevator == null)
                {
                    foreach (var counter in counters)
                    {
                        if (counter != null)
                            ResetCounter(counter);
                    }
                    return;
                }

                var topElevator = GetTopElevator(bottomElevator);
                var isPowered = IsPowered(topElevator);
                var currentFloor = GetDisplayFloor(topElevator);

                foreach (var counter in counters)
                {
                    if (counter == null)
                        continue;

                    if (isPowered)
                        InitializeCounter(counter, currentFloor);
                    else
                        ResetCounter(counter);
                }
            });
        }

        private void OnEntityKill(ElevatorLift lift)
        {
            liftTimerActions.Remove(lift.net.ID);
        }

        private void OnElevatorMove(Elevator topElevator, int targetFloor)
        {
            if (!topElevator.liftEntity.TryGet(true, out var lift)
                || lift == null)
                return;

            var liftFloor = topElevator.LiftPositionToFloor();
            if (targetFloor == liftFloor)
                return;

            // Using NextTick to wait for the movement to begin so we can get the travel time
            NextTick(() =>
            {
                if (topElevator == null || lift == null)
                    return;

                var travelTime = GetTravelTime(lift);
                if (travelTime == 0)
                    return;

                var counters = GetAllConnectedCounters(topElevator);
                if (counters != null)
                {
                    StartUpdatingLiftCounters(lift, counters, travelTime);
                }
            });
        }

        private object OnCounterModeToggle(PowerCounter counter, BasePlayer player, bool doShowPassthrough)
        {
            // Make "show counter" action call the elevator
            var elevator = GetConnectedElevator(counter);
            if (!doShowPassthrough && elevator != null && IsEligibleToBeElevatorCounter(counter))
            {
                elevator.CallElevator();
                return false;
            }

            return null;
        }

        private void OnInputUpdate(PowerCounter counter, int inputAmount)
        {
            // Ignore counters not connected to elevators
            Elevator elevator = GetConnectedElevator(counter);
            if (elevator == null)
                return;

            // This has to be delayed since clearing a wire causes this to be called before the connection is actually removed
            NextTick(() =>
            {
                if (elevator == null || counter == null || HasConnectedInput(counter))
                    return;

                MaybeToggleCounter(elevator, counter);
            });
        }

        private void OnInputUpdate(Elevator elevator, int inputAmount, int inputSlot)
        {
            var counter = elevator.inputs[inputSlot].connectedTo.Get() as PowerCounter;
            if (counter == null)
                return;

            NextTick(() =>
            {
                if (elevator == null || counter == null || HasConnectedInput(counter))
                    return;

                if (GetConnectedElevator(counter) == null)
                {
                    ResetCounter(counter);
                    return;
                }

                MaybeToggleCounter(elevator, counter);
            });
        }

        // Covers the case when power state changes, and when a new elevator is added
        private void OnInputUpdate(ElevatorIOEntity elevatorIOEntity, int inputAmount)
        {
            if (elevatorIOEntity == null)
                return;

            var topElevator = elevatorIOEntity.GetParentEntity() as Elevator;

            // This shouldn't happen normally, but it's possible if elevators were pasted incorrectly
            if (topElevator == null)
                return;

            var counters = GetAllConnectedCounters(topElevator);
            if (counters == null)
                return;

            var bottomElevator = GetBottomElevator(topElevator);

            // BetterElevators uses NextTick to restore power after disconnecting a powerless elevator
            // So this uses a timer (slower) so we can properly determine if the elevator has power
            timer.Once(0, () =>
            {
                if (bottomElevator == null)
                {
                    foreach (var counter in counters)
                    {
                        if (counter != null)
                            ResetCounter(counter);
                    }

                    return;
                }

                topElevator = GetTopElevator(bottomElevator);
                var isPowered = IsPowered(topElevator);
                var currentFloor = GetDisplayFloor(topElevator);

                foreach (var counter in counters)
                {
                    if (isPowered)
                        InitializeCounter(counter, currentFloor);
                    else
                        ResetCounter(counter);
                }
            });
        }

        #endregion

        #region Helper Methods

        private static Elevator GetOwnerElevator(ElevatorLift lift)
        {
            return ElevatorLiftOwnerProperty?.GetValue(lift) as Elevator;
        }

        private float GetTravelTime(ElevatorLift lift)
        {
            var tweens = LeanTween.descriptions(lift.gameObject);
            if (tweens.Length == 0)
                return 0;

            return tweens[0].time;
        }

        private void StartUpdatingLiftCounters(ElevatorLift lift, PowerCounter[] counters, float timeToTravel)
        {
            Action existingTimerAction;
            if (liftTimerActions.TryGetValue(lift.net.ID, out existingTimerAction))
            {
                lift.CancelInvoke(existingTimerAction);
            }

            var lastCounterUpdateTime = Time.time;
            Action timerAction = null;
            var stepsRemaining = timeToTravel / MaxCounterUpdateFrequency;
            timerAction = () =>
            {
                stepsRemaining--;

                var reachedEnd = stepsRemaining <= 0;
                if (reachedEnd || Time.time >= lastCounterUpdateTime + MaxCounterUpdateFrequency)
                {
                    UpdateCounters(lift, counters);
                    lastCounterUpdateTime = Time.time;
                }

                if (reachedEnd)
                {
                    lift.CancelInvoke(timerAction);
                    liftTimerActions.Remove(lift.net.ID);
                }
            };
            lift.InvokeRepeating(timerAction, MaxCounterUpdateFrequency, MaxCounterUpdateFrequency);
            liftTimerActions[lift.net.ID] = timerAction;
        }

        private void UpdateCounters(ElevatorLift lift, PowerCounter[] counters)
        {
            // Get the elevator on every update, since the lift can be re-parented
            var elevator = GetOwnerElevator(lift);
            if (elevator == null || counters == null)
                return;

            var floor = elevator.LiftPositionToFloor() + 1;

            foreach (var counter in counters)
            {
                if (counter.counterNumber == floor)
                    continue;

                counter.counterNumber = floor;
                counter.targetCounterNumber = floor;
                counter.currentEnergy = floor;
                counter.SendNetworkUpdate();
            }
        }

        private int GetDisplayFloor(Elevator topElevator)
        {
            if (!topElevator.liftEntity.TryGet(true, out var liftEntity)
                || liftEntity == null)
                return 1;

            return topElevator.LiftPositionToFloor() + 1;
        }

        private bool IsPowered(Elevator topElevator)
        {
            return topElevator.ioEntity != null && topElevator.ioEntity.IsPowered();
        }

        private Elevator GetTopElevator(Elevator elevator)
        {
            return GetFarthestElevatorInDirection(elevator, Elevator.Direction.Up);
        }

        private Elevator GetBottomElevator(Elevator elevator)
        {
            return GetFarthestElevatorInDirection(elevator, Elevator.Direction.Down);
        }

        private Elevator GetFarthestElevatorInDirection(Elevator elevator, Elevator.Direction direction)
        {
            var currentElevator = elevator;

            Elevator nextElevator;
            while ((nextElevator = currentElevator.GetElevatorInDirection(direction)) != null)
                currentElevator = nextElevator;

            return currentElevator;
        }

        private void MaybeToggleCounter(Elevator elevator, PowerCounter counter)
        {
            var topElevator = GetTopElevator(elevator);
            if (IsPowered(topElevator))
            {
                InitializeCounter(counter, GetDisplayFloor(topElevator));
            }
            else
            {
                ResetCounter(counter);
            }
        }

        private void InitializeCounter(PowerCounter counter, int floor)
        {
            counter.SetFlag(IOEntity.Flag_HasPower, true);
            counter.SetFlag(BaseEntity.Flags.Reserved2, true);
            counter.currentEnergy = floor;
            counter.SendNetworkUpdate();
        }

        private void ResetCounter(PowerCounter counter)
        {
            counter.SetFlag(IOEntity.Flag_HasPower, false);
            counter.counterNumber = 0;
            counter.currentEnergy = 0;
            counter.SendNetworkUpdate();
        }

        private Elevator GetConnectedElevator(PowerCounter counter)
        {
            return counter.outputs[0].connectedTo.Get() as Elevator;
        }

        private bool IsEligibleToBeElevatorCounter(PowerCounter counter)
        {
            // Ignore parented counters such as the lift counter
            if (counter.HasParent())
                return false;

            if (HasConnectedInput(counter))
                return false;

            return true;
        }

        private bool HasConnectedInput(PowerCounter counter)
        {
            return counter.inputs[0].connectedTo.Get() != null;
        }

        private PowerCounter[] GetAllConnectedCounters(Elevator topElevator)
        {
            var counters = new List<PowerCounter>();
            var currentElevator = topElevator;

            do
            {
                GetConnectedCounters(currentElevator, out var counter1, out var counter2);
                if (counter1 != null)
                    counters.Add(counter1);

                if (counter2 != null)
                    counters.Add(counter2);
            }
            while ((currentElevator = currentElevator.GetElevatorInDirection(Elevator.Direction.Down)) != null);

            return counters.Count > 0 ? counters.ToArray() : null;
        }

        private void GetConnectedCounters(Elevator elevator, out PowerCounter counter1, out PowerCounter counter2)
        {
            counter1 = GetEligibleElevatorCounter(elevator.inputs[0].connectedTo.Get() as PowerCounter);
            counter2 = GetEligibleElevatorCounter(elevator.inputs[1].connectedTo.Get() as PowerCounter);
        }

        private PowerCounter GetEligibleElevatorCounter(PowerCounter counter)
        {
            if (counter == null)
                return null;

            // Ignore parented counters such as the lift counter
            if (counter.HasParent())
                return null;

            // Ignore counters that have a connected input
            if (counter.inputs[0].connectedTo.Get() != null)
                return null;

            return counter;
        }

        #endregion
    }
}
