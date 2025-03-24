using Newtonsoft.Json;
using System;

// CREDITS
// 0.1.2 - WhiteThunder: allow fuel amount to exceed vanilla fuel amount
// 0.1.1 - WhiteThunder: separate fuel amount setting for ScrapTransportHelicopter
// 0.0.4 - WhiteThunder: restrict fuel amount changes to vendor-spawned vehicles
// 0.0.3 - Orange: various NRE and resource fixes

namespace Oxide.Plugins
{
    [Info("Air Fuel", "WhiteDragon", "0.2.3")]
    [Description("Sets the initial amount of fuel for vendor purchased air vehicles.")]
    public class AirFuel : CovalencePlugin
    {
        private static AirFuel _instance;

        #region _configuration_

        private static Configuration config;

        private class Configuration
        {
            public Fuel.Settings    Fuel;
            public Version.Settings Version;

            private static bool corrupt  = false;
            private static bool dirty    = false;

            public static void Clamp<T>(ref T value, T min, T max) where T : IComparable<T>
            {
                T clamped = Generic.Clamp(value, min, max);

                if(!value.Equals(clamped))
                {
                    dirty = true; value = clamped;
                }
            }

            public static void Load()
            {
                dirty = false;

                try
                {
                    config = _instance.Config.ReadObject<Configuration>();

                    config.Version.Compare(0, 0, 0);
                }
                catch(NullReferenceException)
                {
                    _instance.Puts("Configuration: Created new configuration with default settings.");

                    dirty = true; config = new Configuration();
                }
                catch(JsonException e)
                {
                    _instance.Puts($"Configuration: Using default settings. Delete the configuration file, or fix the following error, and reload; {e}");

                    corrupt = true; config = new Configuration();
                }

                Validate();
            }

            public static void Save()
            {
                if(dirty && !corrupt)
                {
                    dirty = false;

                    _instance.Config.WriteObject(config);
                }
            }

            public static void SetDirty() => dirty = true;

            public static void Unload()
            {
                Save();

                config = null;
            }

            public static void Validate<T>(ref T value, Func<T> initializer, Action validator = null)
            {
                if(value == null)
                {
                    dirty = true; value = initializer();
                }
                else
                {
                    validator?.Invoke();
                }
            }
            private static void Validate()
            {
                Validate(ref config.Fuel,    () => new Fuel.Settings(), () => config.Fuel.Validate());
                Validate(ref config.Version, () => new Version.Settings());

                config.Version.Validate();

                Save();
            }
        }

        #endregion _configuration_

        #region _fuel_

        private class Fuel
        {
            public class Settings
            {
                public int Default;
                public int MiniCopter;
                public int ScrapTransportHelicopter;

                public Settings()
                {
                    Default                  = 100;
                    MiniCopter               =  -1;
                    ScrapTransportHelicopter =  -1;
                }

                public void Validate()
                {
                    Configuration.Clamp(ref Default,                   0, int.MaxValue);
                    Configuration.Clamp(ref MiniCopter,               -2, int.MaxValue);
                    Configuration.Clamp(ref ScrapTransportHelicopter, -2, int.MaxValue);
                }
            }
        }

        #endregion _fuel_

        #region _generic_

        private class Generic
        {
            public static T Clamp<T>(T value, T min, T max) where T : IComparable<T>
            {
                if(value.CompareTo(min) < 0)
                {
                    return min;
                }
                else if(value.CompareTo(max) > 0)
                {
                    return max;
                }

                return value;
            }
        }

        #endregion _generic_

        #region _hooks_

        private void Init()
        {
            Unsubscribe(nameof(OnEntitySpawned));

            _instance = this;

            Configuration.Load();
        }

        protected override void LoadDefaultConfig() { }

        private void OnServerInitialized()
        {
            Subscribe(nameof(OnEntitySpawned));
        }

        private void OnEntitySpawned(Minicopter vehicle)
        {
            NextTick(() =>
            {
                if(vehicle.creatorEntity == null)
                {
                    return;
                }

                var fuelsystem = vehicle?.GetFuelSystem();
                if(fuelsystem is EntityFuelSystem entityFuelSystem)
                {
                    var fuelAmount = vehicle is ScrapTransportHelicopter ?
                        config.Fuel.ScrapTransportHelicopter :
                        config.Fuel.MiniCopter;

                    if(fuelAmount == -1)
                    {
                        fuelAmount = config.Fuel.Default;
                    }
                    else if(fuelAmount == -2)
                    {
                        return;
                    }

                    var fuelItem = entityFuelSystem.GetFuelItem();
                    if(fuelItem != null && fuelItem.amount != fuelAmount)
                    {
                        fuelItem.amount = fuelAmount;
                        fuelItem.MarkDirty();
                    }
                }
            });
        }

        private void Unload()
        {
            Configuration.Unload();

            _instance = null;
        }

        #endregion _hooks_

        #region _version_

        private new class Version
        {
            public class Settings
            {
                public int Major;
                public int Minor;
                public int Patch;

                public Settings()
                {
                    Major = Minor = Patch = 0;
                }

                public int Compare(int major, int minor, int patch)
                {
                    return
                        (Major != major) ? (Major - major) :
                        (Minor != minor) ? (Minor - minor) :
                        (Patch != patch) ? (Patch - patch) : 0;
                }

                public void Validate()
                {
                    var current = (_instance as CovalencePlugin).Version;

                    if(Compare(current.Major, current.Minor, current.Patch) < 0)
                    {
                        Configuration.SetDirty();

                        Major = current.Major;
                        Minor = current.Minor;
                        Patch = current.Patch;
                    }
                }
            }
        }

        #endregion _version_
    }
}