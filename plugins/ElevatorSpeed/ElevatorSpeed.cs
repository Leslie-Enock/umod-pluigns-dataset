using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Elevator Speed", "Lincoln", "1.0.6")]
    [Description("Adjust the speed of the elevator.")]
    public class ElevatorSpeed : RustPlugin
    {
        private const string PermUse = "ElevatorSpeed.use";
        private const string PermAdmin = "ElevatorSpeed.admin";
        private const int MinSpeed = 1;
        private static Configuration config;

        private class Configuration
        {
            [JsonProperty("Maximum Speed")]
            public int MaximumSpeed { get; set; } = 10;

            public static Configuration DefaultConfig() => new Configuration();
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating new configuration file!");
            config = Configuration.DefaultConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config, true);
        }

        private void OnServerInitialized()
        {
            permission.RegisterPermission(PermUse, this);
            permission.RegisterPermission(PermAdmin, this);
        }

        private List<Elevator> FindElevators(BasePlayer player, float radius = 3f)
        {
            var hits = Physics.SphereCastAll(player.transform.position, radius, Vector3.up);
            var elevators = new List<Elevator>();

            foreach (var hit in hits)
            {
                var elevator = hit.GetEntity()?.GetComponent<Elevator>();
                if (elevator != null && !elevators.Contains(elevator))
                    elevators.Add(elevator);
            }

            return elevators;
        }

        [ChatCommand("ls")]
        private void LiftSpeedCommandAlt(BasePlayer player, string command, string[] args) =>
            CmdLiftSpeed(player, command, args);

        [ChatCommand("liftspeed")]
        private void LiftSpeedCommand(BasePlayer player, string command, string[] args) =>
            CmdLiftSpeed(player, command, args);

        [ChatCommand("lc")]
        private void LiftCheckCommandAlt(BasePlayer player, string command, string[] args) =>
            CmdLiftCheck(player, command, args);

        [ChatCommand("liftcheck")]
        private void LiftCheckCommand(BasePlayer player, string command, string[] args) =>
            CmdLiftCheck(player, command, args);

        private void CmdLiftSpeed(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermUse))
            {
                Message(player, "NoPerm");
                return;
            }

            if (args.Length == 0 || !int.TryParse(args[0], out int speed))
            {
                Message(player, "SpeedInvalid", config.MaximumSpeed);
                return;
            }

            var elevators = FindElevators(player);
            if (elevators.Count == 0)
            {
                Message(player, "NoElevators");
                return;
            }

            if (speed < MinSpeed || (!permission.UserHasPermission(player.UserIDString, PermAdmin) && speed > config.MaximumSpeed))
            {
                Message(player, "SpeedInvalid", config.MaximumSpeed);
                return;
            }

            bool success = false;
            foreach (var elevator in elevators)
            {
                if (permission.UserHasPermission(player.UserIDString, PermAdmin) || elevator.OwnerID == player.userID)
                {
                    elevator.LiftSpeedPerMetre = speed;
                    success = true;
                }
            }

            Message(player, success ? "SpeedUpdate" : "NotOwner", speed);
        }

        private void CmdLiftCheck(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermUse))
            {
                Message(player, "NoPerm");
                return;
            }

            var elevators = FindElevators(player);
            if (elevators.Count == 0)
            {
                Message(player, "NoElevators");
                return;
            }

            var elevator = elevators[0];
            if (permission.UserHasPermission(player.UserIDString, PermAdmin) || elevator.OwnerID == player.userID)
                Message(player, "SpeedCheck", elevator.LiftSpeedPerMetre);
            else
                Message(player, "NotOwner");
        }

        private void Message(BasePlayer player, string messageKey, params object[] args) =>
            player.ChatMessage(string.Format(lang.GetMessage(messageKey, this, player.UserIDString), args));

        protected override void LoadDefaultMessages() => lang.RegisterMessages(new Dictionary<string, string>
        {
            ["NoElevators"] = "<color=#ffc34d>Elevator Speed: </color> No owned elevators found. Please stand near by your elevator.",
            ["NotOwner"] = "<color=#ffc34d>Elevator Speed: </color> You do not own this elevator.",
            ["SpeedUpdate"] = "<color=#ffc34d>Elevator Speed: </color>Updating elevator speed to <color=#b0fa66>{0}</color>.",
            ["SpeedCheck"] = "<color=#ffc34d>Elevator Speed: </color>This elevator speed is set to <color=#b0fa66>{0}</color>.",
            ["NoPerm"] = "<color=#ffc34d>Elevator Speed</color>: You do not have permissions to use this.",
            ["SpeedInvalid"] = "<color=#ffc34d>Elevator Speed</color>: Please choose a speed between <color=#b0fa66>1</color> and <color=#b0fa66>{0}</color>. Default speed is <color=#b0fa66>1</color>.",
        }, this);
    }
}
