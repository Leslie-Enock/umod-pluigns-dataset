using System;
using ConVar;
using System.Collections.Generic;
using Random = System.Random;


namespace Oxide.Plugins
{
    [Info("Tides", "Regr3tti", "0.1.7")]
    [Description("Adds natural tides to rust")]
    public class Tides : RustPlugin
    {
        bool debug;
        bool storms;
        bool currentstorm;
        Timer stormTimer;
        string variance;
        double amt_variance;
        float variance_max;
        float variance_min;
        float tideheight_raw;
        float Tideheight_plus(float variance_low, float variance_high) => tideheight_raw + (((float)random.NextDouble() * (variance_high - variance_low)) + variance_low); // formula for the random variance that adds it to tide height specified in the config for your new actual tide height. If Variance is set to none in the config this will just return the tide height value from the config.  
        float period;
        float offset;
        float tideamplitude;
        float stormtideheight;
        float stormchance;
        private bool Changed;

        float refresh;
        Random random = new Random();
        float GetOceanLevel(float tideheight, float period, float offset) // Where the magic happens, this is what calculates the ocean height based on the time of day Env.time
        {
            return (float)Math.Round((tideheight / 2) * (float)Math.Sin((Math.PI / period) * Env.time - 1f * (Math.PI / offset)) + (tideheight / 2), 3); // Sine wave equation. By default Ocean Level = 1*sin((pi/6)x-(pi/2))+1, where x is Env.time. Simply this makes High Tide at 6:00 and 18:00, with low tide at 0:00 and 12:00. Ocean Levels stay between 0 and 2.
        }
        object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }
        void LoadVariables()
        {
            tideheight_raw = Convert.ToInt32(GetConfig("1 - Tides", "1 - Height. How many meters (1m = 3ft) high do you want High Tide? 2 is natural and default, 5 is high, higher than 10 may have unintended consequences", 2));
            period = Convert.ToInt32(GetConfig("1 - Tides", "2 - Hours between high and low tide. 6 is natural and mimics real life, giving a high tide every 30 minutes (12 in-game hours)", 6));
            offset = Convert.ToInt32(GetConfig("1 - Tides", "4 - Offset. Don't change this unless you know what you're doing, 2 makes low tide start at midnight and -2 makes high tide start at midnight", 2));
            refresh = Convert.ToInt32(GetConfig("1 - Tides", "3 - Refresh rate, so 0.1 happens 10 times per second", 0.01));
            variance = Convert.ToString(GetConfig("2 - Variance", "Defines the amount of variance between high tides. Options are None, Low, Medium, High", "none"));
            debug = Convert.ToBoolean(GetConfig("4 - Debug", "This will output messages to your console to help diagnose problems", true));
            storms = Convert.ToBoolean(GetConfig("3 - Storms", "1 - If this is enabled you'll get a chance of storms", false));
            stormtideheight = Convert.ToInt32(GetConfig("3 - Storms", "2 - Set the maximum height of tides during a storm event. Default is 10 for a 10 meter high tide", 10));
            stormchance = Convert.ToInt32(GetConfig("3 - Storms", "3 - Set the chance of a storm occuring when storms are enabled. 0 means never 100 means always. Default is 10 for a 10% chance of storms", 10));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }
        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }
        void Init()
        {
            LoadVariables();
        }
        void OnServerInitialized()
        {
            CheckVariance();
            float occurance = (float)Math.Round((period * 2), 2);
            Timer oceanTimer = timer.Repeat(refresh, 0, () => //The code below runs an infinite number of times (or until it breaks!) at the specified refresh interval. This is what actually changes the ocean level. 
            {
                OceanLevelUpdater();
            });
            if (debug) { Puts($"Your next High Tide will reach **{Math.Round(tideamplitude, 2)}** meters."); }
            if (debug) { Puts($"The current time is **{TimeSpan.FromHours(Math.Round(Env.time, 2))}**"); }
            if (debug) { Puts($"The current ocean height is **{Math.Round(Env.oceanlevel, 2)}** meters"); }
            if (debug) { Puts($"Storms are set to {storms}"); }
            if (storms) { if (debug) { Puts($"Your chance of a storm occuring are {stormchance}%"); } }
            if (storms) { if (debug) { Puts($"During a storm event, your high tide will reach {stormtideheight} meters"); } }
            //if (Env.time < GetHighTide(period, tideamplitude, 0))
            //{
            //    if (debug) { Puts($"Your next High Tide will be at {TimeSpan.FromHours(GetHighTide(period, tideamplitude, 0)):h\\:mm}, with a new High Tide every {Math.Round(period * 2)} in-game hours"); }
            //}
            //else
            //{
            //    if (debug) { Puts($"Your next High Tide will be at {TimeSpan.FromHours(Math.Round(GetHighTide(period, tideamplitude, occurance), 2)):h\\:mm}, with a new High Tide every {Math.Round(period * 2)} in-game hours"); }
            //}
        }
        void OceanLevelUpdater()
        {
            ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), $"env.oceanlevel {GetOceanLevel(tideamplitude, period, offset)}"); //Executes the command, and blocks output to the console (or you'd get a flood of commands every 0.1 seconds)
            if (variance.Contains("low") || variance.Contains("medium") || variance.Contains("high") || storms)
            {
                CheckForLowTide();
                //Puts("Either Storms or Variance is enabled... checking for low tide");
            }
        }
        void CheckForLowTide()
        {
            bool itslowtide = (Math.Abs(Env.time % (period * 2)) < 0.0004 || Math.Abs(Env.time) < 0.001);
            if (itslowtide) // Checks that the oceanlevel is 0, which is the safest time to change the next tide height. There's an extra check for midnight so there's no divide by 0 error. This only does something if variance is enabled. 
            {
                DestroyTimer(stormTimer);
                currentstorm = false;
                ChanceOfStorm();
            } 
        }
        void DestroyTimer(Timer timer)
        {
            timer?.DestroyToPool();
            timer = null;
        }
        void ChanceOfStorm() //Rolling the dice on whether or not there's a storm event. 
        {
            if (random.NextDouble() < (stormchance / 100))
            {
                tideamplitude = stormtideheight;
                currentstorm = true;
                Puts("Chances of a storm coming are high...");
                stormTimer = timer.Repeat(1, 0, () => //The code below runs an infinite number of times (or until it breaks!) at the specified refresh interval. This is what actually changes the ocean level. 
                {
                    CreateStorms();
                });
            }    
            if (!currentstorm)
            {
                ChangeHighTide(); // This redoes the variance calculation to return a new maximum ocean height.
                if (debug) { Puts($"Your Variance setting is {variance},it's currently Low Tide and the time is {TimeSpan.FromHours(Env.time)} . Rolling the dice a few times for your next High Tide height... {Math.Round(tideamplitude, 2)} meters"); }
            }
        }
        void ChangeHighTide()
        {
            tideamplitude = Tideheight_plus(variance_min, variance_max);
        }
        float GetHighTide(float period, float tideamplitude, float occurance)
        {
            return (float)Math.Round((period / 2) + ((period / Math.PI)*(Math.Asin(tideamplitude - 1))) + (occurance));
        }
        void CheckVariance()
        {
            switch (variance.ToLower()) //checks for the config setting for variance, sets the value that tide height will be divided by to calculate the limits of the variance added to your defined tide height in the config. If 0 is selected, no variance is added.
            {
                case "none":
                    variance_max = 0;
                    variance_min = 0;
                    if (debug) { Puts($"Variance is set to **{variance.ToLower()}**, so your current tide height will never change. If you expected the opposite, change your Variance setting in the config file to **Low**, **Medium**, or **High**."); }
                    break;
                case "low":
                    amt_variance = 9.0;
                    SetVariance();
                    break;
                case "medium":
                    amt_variance = 5.0;
                    SetVariance();
                    break;
                case "high":
                    amt_variance = 1.2;
                    SetVariance();
                    break;
                default:
                    if (debug) { Puts($"The Variance setting in the Config file is wrong. You set it to **{variance.ToUpper()}**, and acceptable options are NONE, LOW, MEDIUM, HIGH. Capitalization doesn't matter."); }
                    break;
            }
           ChangeHighTide(); // Makes sure High Tide is set when the plugin loads. 
        }
        void SetVariance() //checks what value Variance is set to in the config and calculates the high and low boundaries for the variance calculation
            {
                variance_max = (float)(tideheight_raw / amt_variance); //The highest variance you might get in max tide height (e.g. you set a tide of 2, the tide might be as high as 2.22 in a given 15 minute period)
                variance_min = (float)(-1 * (tideheight_raw / amt_variance)); //The lowest variance you might get in max tide height (e.g. you set a tide of 2, the tide might be as low as 1.78 in a given 15 minute period)
                if (debug) { Puts($"Average High Tide height is set to **{Math.Round(tideheight_raw)}**, Variance is set to **{variance.ToUpper()}**. Due to your settings your next high tide will be between **{Math.Round(tideheight_raw - variance_min, 2)}** meters and **{Math.Round(tideheight_raw - variance_max, 2)}** meters.  If you expected less or no variance in your High Tide heights, please change your Variance settings in the config file"); }

            }
        void CreateStorms()
        {
                ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), $"weather.fog {GetOceanLevel(1f, period, offset)}"); //Executes the command, and blocks output to the console (or you'd get a flood of commands every 0.1 seconds)
                ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), $"weather.clouds {GetOceanLevel(1f, period, offset)}");
                ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), $"weather.rain {GetOceanLevel(1f, period, offset)}");
                ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), $"weather.wind {GetOceanLevel(1f, period, offset)}");

        }
    }
}