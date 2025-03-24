using System;
using System.Collections.Generic;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Clear Weather", "Rick", "1.1.0")]
    [Description("Always clear weather")]

    public class ClearWeather : RustPlugin
    {
        void OnServerInitialized()
        {
            ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "weather.load clear");
            ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "weather.rain 0");
            ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "weather.fog 0");
			ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "weather.thunder 0");
            ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "weather.storm_chance 0");
			ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "weather.overcast_chance 0");
			ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "weather.rain_chance 0");
        }

        void Unload()
        {
            ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "weather.reset");
        }
    }
}