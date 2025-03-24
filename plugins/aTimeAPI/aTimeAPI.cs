
/*Copyright © 2022 - 2023 AvG Лаймон(Email: alias.dev@ya.ru | Discord: AvG Лаймон#0680 | Alias™ development team: https://discord.gg/MWeNJV5e7F ) */

using Newtonsoft.Json;
using Oxide.Core;
using System;
using System.Collections;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("aTimeAPI", "AvG Лаймон", "1.1.3")]
    [Description("Provides API and Hooks for time.")]
    class aTimeAPI : RustPlugin
    {
        #region [Initialization]
        private static DataFileSystem fileSystem = Interface.Oxide.DataFileSystem;
        private void OnServerInitialized()
        {
            data = fileSystem.GetFile(Name).ReadObject<Data>();
            if (data == null)
            {
                data = new Data();
                fileSystem.GetFile(Name).WriteObject(data);
            }
            CheckTimeCoroutine = ServerMgr.Instance.StartCoroutine(CheckTime());
        }
        private void Unload()
        {
            if (CheckTimeCoroutine != null)
                ServerMgr.Instance.StopCoroutine(CheckTimeCoroutine);
            fileSystem.GetFile(Name).WriteObject(data);
        }
        #endregion
        #region [Data]
        private Data data;
        private class Data
        {
            public bool IsDay = true;
            public int Year = DateTime.Now.Year;
            public int Month = DateTime.Now.Month;
            public int Day = DateTime.Now.Day;
            public int Hour = DateTime.Now.Hour;
        }
        #endregion
        #region [Configuration]
        private Configuration config;
        private class Configuration
        {
            [JsonProperty("Rust day start time (hour)")] public float Day = 7.5f;
            [JsonProperty("Rust night start time (hour)")] public float Night = 20;
        }
        protected override void LoadDefaultConfig() => config = new Configuration();
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<Configuration>();
            SaveConfig();
        }
        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion
        private bool IsDayInRustNow() => data.IsDay;
        private Coroutine CheckTimeCoroutine = null;
        private IEnumerator CheckTime()
        {
            while (true)
            {
                Interface.CallHook("OnRealSecond");
                float hour = TOD_Sky.Instance.Cycle.Hour;
                if (data.IsDay)
                {
                    if (hour >= config.Night && hour < config.Day)
                    {
                        data.IsDay = false;
                        Interface.CallHook("OnRustNightStarted");
                    }
                }
                else
                {
                    if (hour >= config.Day && hour < config.Night)
                    {
                        data.IsDay = true;
                        Interface.CallHook("OnRustDayStarted");
                    }
                }
                if (DateTime.Now.Hour != data.Hour)
                {
                    data.Hour = DateTime.Now.Hour;
                    Interface.CallHook("OnNewRealHourStarted", data.Hour);
                    if (DateTime.Now.Day != data.Day)
                    {
                        data.Day = DateTime.Now.Day;
                        Interface.CallHook("OnNewRealDayStarted", data.Day);
                        if (DateTime.Now.Month != data.Month)
                        {
                            data.Month = DateTime.Now.Month;
                            Interface.CallHook("OnNewRealMonthStarted", data.Month);
                            if (DateTime.Now.Year != data.Year)
                            {
                                data.Year = DateTime.Now.Year;
                                Interface.CallHook("OnNewRealYearStarted", data.Year);
                            }
                        }
                    }
                    fileSystem.GetFile(Name).WriteObject(data);
                }
                yield return new WaitForSeconds(1);
            }
        }
    }
}
