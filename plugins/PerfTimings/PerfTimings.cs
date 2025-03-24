using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Oxide.Plugins
{

    [Info("Perf Timings", "Rockioux", "1.0.0")]
    [Description("Rudimentary performance timer allowing to get min, max, cumul and average processing time for a block of code. Meant to be used by plugin developers.")]
    public class PerfTimings : CovalencePlugin
    {

        #region Inner Classes

        /// <summary>
        /// Disposable object to use to record performance timings. Writes the time
        /// it took to execute once the object is disposed of.
        /// </summary>
        public class PerfTimer : IDisposable
        {
            private readonly Stopwatch _timer;
            private readonly PerfTimings _plugin;
            private readonly string _key;

            public PerfTimer(PerfTimings plugin, string key)
            {
                _timer = new Stopwatch();
                _timer.Start();
                _plugin = plugin;
                _key = key;
            }

            public void Dispose()
            {
                _timer.Stop();
                _plugin.AddPerfMetrics(_key, _timer.Elapsed.TotalMilliseconds);
            }
        }

        /// <summary>
        /// Contains the metrics gathered for a specific metrics's key.
        /// </summary>
        private class PerfMetrics
        {
            public double Cumul { get; private set; } = 0;
            public double Min { get; private set; } = double.MaxValue;
            public double Max { get; private set; } = 0;
            public int TotalCalls { get; private set; } = 0;
            public double Avg => Cumul / TotalCalls;

            public void AddValue(double value)
            {
                Cumul += value;
                Min = Math.Min(Min, value);
                Max = Math.Max(Max, value);
                TotalCalls++;
            }
        }

        #endregion // Inner Classes

        #region Fields

        private readonly Dictionary<string, PerfMetrics> _perfMetricsByTag = new Dictionary<string, PerfMetrics>();
        private PerfMetrics _framePerfMetrics;
        private double _captureDurationMs = 0;
        private int _maxKeyStrLen = 0;
        private bool _isCurrentlyCapturing = false;

        private const double kDefaultCaptureDurationMs = 30000;
        private const int kColumnAlignment = 8;

        #endregion // Fields

        #region Public Methods

        /// <summary>
        /// Function used to create a performance timer to measure performance on a 
        /// bit of code. Timer start at the timer's creation, and stops when the timer is disposed of
        /// (usually when it runs out of a using block).
        /// </summary>
        /// <param name="tags">Tags to be merged together that will be the unique name for the timer</param>
        /// <returns>Performance timer</returns>
        public PerfTimer CreatePerfTimer(params string[] tags)
        {
            string key = string.Join("::", tags);
            return _isCurrentlyCapturing ? new PerfTimer(this, key) : null;
        }

        #endregion // Public Methods

        #region Commands

        /// <summary>
        /// Command used to start a timed performance capture.
        /// </summary>
        /// <param name="player">Requester</param>
        /// <param name="command">Command</param>
        /// <param name="args">Duration of the capture in ms</param>
        [Command("perfcapture"), Permission("perftimings.capture")]
        private void PerfTimingsCapture(IPlayer player, string command, string[] args)
        {
            if (args.Length > 1)
            {
                Puts($"Invalid number of arguments {args.Length} when it should be 1");
                return;
            }

            int duration = 0;
            if (args.Length > 0 && !int.TryParse(args[0], out duration))
            {
                Puts($"Invalid duration specified {args[0]}, should be an integer in milliseconds");
                return;
            }

            PerfTimingsCapture_Internal(duration);
        }

        #endregion // Commands

        #region UMod Hooks

        /// <summary>
        /// Captures the frame stats so we can compare timer stats with frame. Useful
        /// to know if there are frame spikes.
        /// </summary>
        /// <param name="delta">Delta is in seconds</param>
        private void OnFrame(float delta)
        {
            if (!_isCurrentlyCapturing) return;

            _framePerfMetrics.AddValue(delta * 1000f);

            if (_framePerfMetrics.Cumul >= _captureDurationMs)
            {
                PrintStats();
                Reset();
            }
        }

        #endregion // UMod Hooks

        #region Private Methods

        /// <summary>
        /// Used by the timer to add it's running duration to the perf metrics class.
        /// </summary>
        /// <param name="key">Unique identifier for the timer</param>
        /// <param name="value">Duration in ms</param>
        private void AddPerfMetrics(string key, double value)
        {
            if (!_perfMetricsByTag.ContainsKey(key))
            {
                _maxKeyStrLen = Math.Max(_maxKeyStrLen, key.Length);
                _perfMetricsByTag[key] = new PerfMetrics();
            }

            _perfMetricsByTag[key].AddValue(value);
        }

        /// <summary>
        /// Resets all the values and clears the performance metrics.
        /// </summary>
        private void Reset()
        {
            _isCurrentlyCapturing = false;
            _framePerfMetrics = null;
            _perfMetricsByTag.Clear();
        }

        /// <summary>
        /// Sets everything for the capture to start.
        /// </summary>
        /// <param name="duration">Duration in ms</param>
        private void PerfTimingsCapture_Internal(int duration = 0)
        {
            _captureDurationMs = duration != 0 ? duration : kDefaultCaptureDurationMs;
            _framePerfMetrics = new PerfMetrics();
            _isCurrentlyCapturing = true;
        }

        /// <summary>
        /// Print the stats for all performance metrics objects. Prints it in one big block since printing lines
        /// in succession caused quite a bit of lag.
        /// </summary>
        private void PrintStats()
        {
            StringBuilder stringBuilder = new StringBuilder();
            string header = $"| {"Name".PadRight(_maxKeyStrLen)} | {"Min",-kColumnAlignment} | {"Max",-kColumnAlignment} | {"Avg",-kColumnAlignment} | {"Cumul",-kColumnAlignment} | {"Calls",-kColumnAlignment} |";
            stringBuilder.AppendLine(header);
            stringBuilder.AppendLine(new string('-', header.Length));

            Func <double, string> formatDuration = duration =>
            {
                string unit = "ms";
                double number = duration;
                if (duration > 1000d)
                {
                    unit = "s";
                    number = duration / 1000d;
                }
                else if (duration < 1d)
                {
                    number = duration * 1000d;
                    unit = "us";

                    if (number < 1d)
                    {
                        number = number * 1000d;
                        unit = "ns";
                    }
                }
                return $"{number:F2}{unit}";
            };

            List<KeyValuePair<string, PerfMetrics>> orderedMetrics = _perfMetricsByTag.OrderByDescending(pair => pair.Value.Cumul).ToList();
            foreach(KeyValuePair<string, PerfMetrics> pair in orderedMetrics)
            {
                stringBuilder.AppendLine($"| {pair.Key.PadRight(_maxKeyStrLen)} | {formatDuration(pair.Value.Min),kColumnAlignment} | {formatDuration(pair.Value.Max),kColumnAlignment} | {formatDuration(pair.Value.Avg),kColumnAlignment} | {formatDuration(pair.Value.Cumul),kColumnAlignment} | {pair.Value.TotalCalls,kColumnAlignment} |");
            }

            stringBuilder.AppendLine(new string('-', header.Length));
            stringBuilder.AppendLine($"| {"Total".PadRight(_maxKeyStrLen)} | {formatDuration(_framePerfMetrics.Min),kColumnAlignment} | {formatDuration(_framePerfMetrics.Max),kColumnAlignment} | {formatDuration(_framePerfMetrics.Avg),kColumnAlignment} | {formatDuration(_framePerfMetrics.Cumul),kColumnAlignment} | {_framePerfMetrics.TotalCalls,kColumnAlignment} |");
            Puts(stringBuilder.ToString());
        }
    }

    #endregion // Private Methods

}
