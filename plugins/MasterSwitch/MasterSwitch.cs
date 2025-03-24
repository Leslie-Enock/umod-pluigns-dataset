using CompanionServer;
using Facepunch;
using System;
using System.Collections.Generic;
using UnityEngine;
using WebSocketSharp;
using static BaseEntity;

namespace Oxide.Plugins
{
    [Info("Master Switch", "Lincoln", "1.1.0")]
    [Description("Toggle things on or off with a command.")]
    public class MasterSwitch : RustPlugin
    {
        private const float MaxValue = 500;
        private const float MinValue = 0f;
        private const string PermUse = "MasterSwitch.use";
        private float radius;
        private int requiredPower;
        private readonly List<Timer> doorTimerList = new List<Timer>();

        #region Permissions

        private void Init()
        {
            permission.RegisterPermission(PermUse, this);
        }

        #endregion

        #region Checks

        private bool HasPermission(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermUse))
            {
                player.ChatMessage(lang.GetMessage("NoPerm", this, player.UserIDString));
                return false;
            }

            return true;
        }

        private bool HasArgs(BasePlayer player, string[] args)
        {
            if (args == null || args.Length < 2 || !float.TryParse(args[1], out radius))
            {
                player.ChatMessage(lang.GetMessage("Syntax", this, player.UserIDString));
                return false;
            }

            return true;
        }

        private bool HasRadius(BasePlayer player, float radius)
        {
            if (radius <= MinValue || radius > MaxValue)
            {
                player.ChatMessage(lang.GetMessage("Radius", this, player.UserIDString));
                return false;
            }

            return true;
        }

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["LightTurnOn"] = "<color=#ffc34d>MasterSwitch</color>: {0} Lights/electric items turned on within a {1}f radius.",
                ["LightTurnOff"] = "<color=#ffc34d>MasterSwitch</color>: {0} Lights/electric items turned off within a {1}f radius.",
                ["DoorOpen"] = "<color=#ffc34d>MasterSwitch</color>: {0} Doors opened within a {1}f radius.",
                ["DoorClose"] = "<color=#ffc34d>MasterSwitch</color>: {0} Doors closed within a {1}f radius.",
                ["TurretStart"] = "<color=#ffc34d>MasterSwitch</color>: {0} Turrets started within a {1}f radius.",
                ["TurretStop"] = "<color=#ffc34d>MasterSwitch</color>: {0} Turrets stopped within a {1}f radius.",
                ["BearTrapArm"] = "<color=#ffc34d>MasterSwitch</color>: {0} Bear traps have been armed within a {1}f radius.",
                ["BearTrapDisArm"] = "<color=#ffc34d>MasterSwitch</color>: {0} Bear traps have been disarmed within a {1}f radius.",
                ["MineExplode"] = "<color=#ffc34d>MasterSwitch</color>: {0} Mines have exploded within a {1}f radius.",
                ["IgnitableLit"] = "<color=#ffc34d>MasterSwitch</color>: {0} Ignitables have been lit within a {1}f radius.",
                ["MachinesOn"] = "<color=#ffc34d>MasterSwitch</color>: {0} Fog/snow machines activated within a {1}f radius.",
                ["MachinesOff"] = "<color=#ffc34d>MasterSwitch</color>: {0} Fog/snow machines de-activated within a {1}f radius.",
                ["NoPerm"] = "<color=#ffc34d>MasterSwitch</color>: You do not have permissions to use this.",
                ["Syntax"] = "<color=#ffc34d>MasterSwitch</color>: Incorrect syntax. Example /ms <command> <radius>",
                ["Radius"] = "<color=#ffc34d>MasterSwitch</color>: Radius out of bounds, choose 1 - 500",
            }, this);
        }

        #endregion

        #region Unity

        private List<BaseEntity> FindBaseEntity(Vector3 pos, float radius)
        {
            var hits = Physics.SphereCastAll(pos, radius, Vector3.up);
            var entities = new List<BaseEntity>();
            foreach (var hit in hits)
            {
                var entity = hit.GetEntity()?.GetComponent<BaseEntity>();
                if (entity && !entities.Contains(entity))
                    entities.Add(entity);
            }

            return entities;
        }

        #endregion

        #region Commands

        [ChatCommand("ms")]
        private void ToggleCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null || !HasPermission(player) || !HasArgs(player, args) || !HasRadius(player, radius)) return;

            var baseEntityList = FindBaseEntity(player.transform.position, radius);
            var entityCount = new List<string>();

            switch (args[0].ToLower())
            {
                case "open":
                    ToggleDoors(baseEntityList, entityCount, true);
                    player.ChatMessage(string.Format(lang.GetMessage("DoorOpen", this, player.UserIDString), entityCount.Count, radius));
                    break;

                case "timer":
                    Puts($"{doorTimerList.Count} active timers running.");
                    break;

                case "close":
                    ToggleDoors(baseEntityList, entityCount, false);
                    player.ChatMessage(string.Format(lang.GetMessage("DoorClose", this, player.UserIDString), entityCount.Count, radius));
                    break;

                case "on":
                    ToggleLightsAndDevices(baseEntityList, entityCount, true);
                    player.ChatMessage(string.Format(lang.GetMessage("LightTurnOn", this, player.UserIDString), entityCount.Count, radius));
                    break;

                case "off":
                    ToggleLightsAndDevices(baseEntityList, entityCount, false);
                    player.ChatMessage(string.Format(lang.GetMessage("LightTurnOff", this, player.UserIDString), entityCount.Count, radius));
                    break;

                case "start":
                    ToggleTurrets(baseEntityList, entityCount, true);
                    player.ChatMessage(string.Format(lang.GetMessage("TurretStart", this, player.UserIDString), entityCount.Count, radius));
                    break;

                case "stop":
                    ToggleTurrets(baseEntityList, entityCount, false);
                    player.ChatMessage(string.Format(lang.GetMessage("TurretStop", this, player.UserIDString), entityCount.Count, radius));
                    break;

                case "arm":
                    ToggleBearTraps(baseEntityList, entityCount, true);
                    player.ChatMessage(string.Format(lang.GetMessage("BearTrapArm", this, player.UserIDString), entityCount.Count, radius));
                    break;

                case "disarm":
                    ToggleBearTraps(baseEntityList, entityCount, false);
                    player.ChatMessage(string.Format(lang.GetMessage("BearTrapDisArm", this, player.UserIDString), entityCount.Count, radius));
                    break;

                case "ignite":
                    IgniteEntities(baseEntityList, entityCount);
                    player.ChatMessage(string.Format(lang.GetMessage("IgnitableLit", this, player.UserIDString), entityCount.Count, radius));
                    break;

                case "explode":
                    ExplodeEntities(baseEntityList, entityCount);
                    player.ChatMessage(string.Format(lang.GetMessage("MineExplode", this, player.UserIDString), entityCount.Count, radius));
                    break;

                case "activate":
                    ToggleMachines(baseEntityList, entityCount, true);
                    player.ChatMessage(string.Format(lang.GetMessage("MachinesOn", this, player.UserIDString), entityCount.Count, radius));
                    break;

                case "deactivate":
                    ToggleMachines(baseEntityList, entityCount, false);
                    player.ChatMessage(string.Format(lang.GetMessage("MachinesOff", this, player.UserIDString), entityCount.Count, radius));
                    break;

                default:
                    player.ChatMessage("Not a valid command type");
                    break;
            }
        }

        private void ToggleDoors(List<BaseEntity> entities, List<string> entityCount, bool open)
        {
            foreach (var entity in entities)
            {
                if (entity is Door door)
                {
                    if (door.IsOpen() == open) continue;
                    door.SetOpen(open);
                    entityCount.Add(door.ToString());
                }
                else if (entity is ProgressDoor progressDoor)
                {
                    if (open)
                    {
                        var doorTimer = timer.Every(0.1f, () =>
                        {
                            if (progressDoor == null) return;
                            progressDoor.AddEnergy(1f);
                            progressDoor.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                        });
                        doorTimerList.Add(doorTimer);
                    }
                    else
                    {
                        foreach (var timer in doorTimerList)
                        {
                            timer.Destroy();
                        }
                        doorTimerList.Clear();
                    }
                    entityCount.Add(progressDoor.ToString());
                }
            }
        }

        private void ToggleLightsAndDevices(List<BaseEntity> entities, List<string> entityCount, bool on)
        {
            foreach (var entity in entities)
            {
                if (entity is BaseOven baseOven)
                {
                    if (baseOven.IsOn() == on) continue;
                    if (on) baseOven.StartCooking();
                    else baseOven.StopCooking();
                    entityCount.Add(baseOven.ToString());
                }
                else if (entity is SirenLight sirenLight)
                {
                    if (sirenLight.IsPowered() == on) continue;
                    sirenLight.SetFlag(BaseEntity.Flags.Reserved8, on, true, true);
                    entityCount.Add(sirenLight.ToString());
                }
                else if (entity is CeilingLight ceilingLight)
                {
                    if (ceilingLight.IsOn() == on) continue;
                    ceilingLight.SetFlag(BaseEntity.Flags.On, on);
                    entityCount.Add(ceilingLight.ToString());
                }
                else if (entity is SearchLight searchLight)
                {
                    if (searchLight.IsPowered() == on) continue;
                    searchLight.SetFlag(BaseEntity.Flags.Reserved8, on, false, true);
                    searchLight.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                    entityCount.Add(searchLight.ToString());
                }
                else if (entity is Candle candle)
                {
                    if (candle.IsOn() == on) continue;
                    candle.SetFlag(BaseEntity.Flags.On, on);
                    entityCount.Add(candle.ToString());
                }
                else if (entity is AdvancedChristmasLights christmasLights)
                {
                    if (christmasLights.IsPowered() == on) continue;
                    christmasLights.SetFlag(BaseEntity.Flags.Reserved8, on, false, true);
                    christmasLights.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                    entityCount.Add(christmasLights.ToString());
                }
                else if (entity is FlasherLight flasherLight)
                {
                    if (flasherLight.IsPowered() == on || flasherLight.IsOn() == on) continue;
                    flasherLight.SetFlag(BaseEntity.Flags.Reserved8, on, false, true);
                    flasherLight.SetFlag(BaseEntity.Flags.On, on, false, true);
                    flasherLight.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                    entityCount.Add(flasherLight.ToString());
                }
                else if (entity is SimpleLight simpleLight)
                {
                    if (simpleLight.IsOn() == on) continue;
                    simpleLight.SetFlag(BaseEntity.Flags.On, on, true);
                    simpleLight.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                    entityCount.Add(simpleLight.ToString());
                }
                else if (entity is ElectricalHeater heater)
                {
                    if (heater.IsPowered() == on) continue;
                    heater.SetFlag(BaseEntity.Flags.Reserved8, on, false, true);
                    heater.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                    entityCount.Add(heater.ToString());
                }
                else if (entity is FuelGenerator fuelGenerator)
                {
                    if (fuelGenerator.IsOn() == on) continue;
                    if (on)
                    {
                        fuelGenerator.currentEnergy = 40;
                        fuelGenerator.outputEnergy = 40;
                        fuelGenerator.fuelPerSec = 0f;
                        fuelGenerator.Init();
                        fuelGenerator.SetFlag(BaseEntity.Flags.On, true, true);
                        var itemDef = ItemManager.FindItemDefinition("lowgradefuel");
                        fuelGenerator.InvokeRepeating(fuelGenerator.UpdateCurrentEnergy, 0f, 1f);
                        fuelGenerator.inventory.AddItem(itemDef, 1);
                    }
                    else
                    {
                        fuelGenerator.SetFlag(BaseEntity.Flags.On, false, true);
                        fuelGenerator.Init();
                    }
                    fuelGenerator.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                    entityCount.Add(fuelGenerator.ToString());
                }
                else if (entity is ElectricFurnaceIO electricFurnaceIO)
                {
                    var parentEntity = electricFurnaceIO.GetParentEntity();
                    if (parentEntity is ElectricOven electricOven)
                    {
                        if (on)
                        {
                            electricFurnaceIO.UpdateHasPower(electricFurnaceIO.PowerConsumption, 0);
                        }
                        else
                        {
                            electricFurnaceIO.UpdateHasPower(0, 0);
                        }
                        entityCount.Add(electricOven.ToString());
                    }
                }
                else if (entity is DeployableBoomBox boomBox)
                {
                    if (boomBox.IsOn() == on) continue;
                    boomBox.SetFlag(BaseEntity.Flags.On, on, true);
                    boomBox.SetFlag(BaseEntity.Flags.Reserved8, on, true, true);
                    boomBox.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                    entityCount.Add(boomBox.ToString());
                }
                else if (entity is IndustrialConveyor conveyor)
                {
                    if (conveyor.IsOn() == on) continue;
                    conveyor.SetFlag(BaseEntity.Flags.Reserved8, on, false, true);
                    conveyor.SetFlag(BaseEntity.Flags.On, on, true);
                    conveyor.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                    entityCount.Add(conveyor.ToString());
                }
                else if (entity is NeonSign neonSign)
                {
                    if (neonSign.IsOn() == on) continue;
                    neonSign.SetFlag(BaseEntity.Flags.On, on, true);
                    neonSign.SetFlag(BaseEntity.Flags.Reserved8, on, false, true);
                    if (on) neonSign.InvokeRepeating(neonSign.animationLoopAction, neonSign.animationSpeed, neonSign.animationSpeed);
                    else neonSign.CancelInvoke(neonSign.animationLoopAction);
                    neonSign.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                    entityCount.Add(neonSign.ToString());
                }
                else if (entity is WaterPump waterPump)
                {
                    if (waterPump.IsOn() == on) continue;
                    waterPump.SetFlag(BaseEntity.Flags.On, on, true);
                    waterPump.SetFlag(BaseEntity.Flags.Reserved8, on, false, true);
                    waterPump.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                    entityCount.Add(waterPump.ToString());
                }
                else if (entity is StrobeLight strobeLight)
                {
                    if (strobeLight.IsOn() == on) continue;
                    if (on) strobeLight.frequency = 20f;
                    strobeLight.SetFlag(BaseEntity.Flags.On, on, true);
                    strobeLight.SetFlag(BaseEntity.Flags.Reserved8, on, false, true);
                    strobeLight.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                    entityCount.Add(strobeLight.ToString());
                }
                else if (entity is FishMount fishMount)
                {
                    if (on) Effect.server.Run("assets/prefabs/misc/decor_dlc/huntingtrophy_fish/effects/hunting-trophy-fish-song.prefab", fishMount.transform.position);
                    fishMount.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                    entityCount.Add(fishMount.ToString());
                }
            }
        }


        private void ToggleTurrets(List<BaseEntity> entities, List<string> entityCount, bool on)
        {
            foreach (var entity in entities)
            {
                if (entity is AutoTurret turret)
                {
                    if (turret.IsOn() == on) continue;
                    if (on) turret.InitiateStartup();
                    else turret.InitiateShutdown();
                    entityCount.Add(turret.ToString());
                }
            }
        }

        private void ToggleBearTraps(List<BaseEntity> entities, List<string> entityCount, bool arm)
        {
            foreach (var entity in entities)
            {
                if (entity is BearTrap bearTrap)
                {
                    if (bearTrap.IsOn() == arm) continue;
                    if (arm) bearTrap.Arm();
                    else bearTrap.Fire();
                    entityCount.Add(bearTrap.ToString());
                }
            }
        }

        private void IgniteEntities(List<BaseEntity> entities, List<string> entityCount)
        {
            foreach (var entity in entities)
            {
                if (entity is BaseFirework firework && !firework.IsLit())
                {
                    firework.SetFlag(BaseEntity.Flags.OnFire, true, false, true);
                    firework.Invoke(firework.Begin, firework.fuseLength);
                    entityCount.Add(firework.ToString());
                }
                else if (entity is Igniter igniter)
                {
                    igniter.IgniteRange = 5f;
                    igniter.IgniteStartDelay = 0;
                    igniter.UpdateHasPower(1, 1);
                    igniter.SetFlag(BaseEntity.Flags.Reserved8, true, true, true);
                    entityCount.Add(igniter.ToString());
                }
                else if (entity is ConfettiCannon confettiCannon)
                {
                    confettiCannon.SetFlag(BaseEntity.Flags.OnFire, true, false, true);
                    confettiCannon.Ignite(confettiCannon.transform.position);
                    confettiCannon.DamagePerBlast = 0f;
                    confettiCannon.BlastCooldown = 0f;
                    entityCount.Add(confettiCannon.ToString());
                }
            }
        }

        private void ExplodeEntities(List<BaseEntity> entities, List<string> entityCount)
        {
            foreach (var entity in entities)
            {
                if (entity is Landmine landmine)
                {
                    landmine.Explode();
                    entityCount.Add(landmine.ToString());
                }
            }
        }

        private void ToggleMachines(List<BaseEntity> entities, List<string> entityCount, bool on)
        {
            foreach (var entity in entities)
            {
                if (entity is FogMachine fogMachine)
                {
                    if (fogMachine.IsOn() == on) continue;
                    if (on)
                    {
                        fogMachine.EnableFogField();
                        fogMachine.StartFogging();
                    }
                    else
                    {
                        fogMachine.FinishFogging();
                        fogMachine.DisableNozzle();
                    }
                    fogMachine.SetFlag(BaseEntity.Flags.On, on);
                    entityCount.Add(fogMachine.ToString());
                }
                else if (entity is SnowMachine snowMachine)
                {
                    if (snowMachine.IsOn() == on) continue;
                    if (on)
                    {
                        snowMachine.EnableFogField();
                        snowMachine.StartFogging();
                    }
                    else
                    {
                        snowMachine.FinishFogging();
                        snowMachine.DisableNozzle();
                    }
                    snowMachine.SetFlag(BaseEntity.Flags.On, on);
                    entityCount.Add(snowMachine.ToString());
                }
            }
        }

        #endregion
    }
}
