using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Coroutines", "birthdates", "3.0.8")]
    [Description(
        "Allows other plugins to spread out large workloads over time to reduce lag spikes")]
    public class Coroutines : CovalencePlugin
    {
        #region Variables

        private readonly LinkedList<KeyValuePair<string, LinkedList<Coroutine>>> _coroutines =
            new LinkedList<KeyValuePair<string, LinkedList<Coroutine>>>();

        private int _currentCoroutineCount;

        private readonly IDictionary<string, Number> _maxCoroutineCounter = new Dictionary<string, Number>();
        private readonly IDictionary<string, int> _idToMax = new Dictionary<string, int>();

        private readonly LinkedList<Coroutine> _queuedCoroutines = new LinkedList<Coroutine>();
        private readonly Stopwatch _stopwatch = new Stopwatch();
        public static Coroutines Instance { get; private set; }

        #endregion

        #region Classes

        /// <summary>
        ///     The main coroutine class that handles instructions via <see cref="Coroutines.OnFrame" />
        /// </summary>
        public class Coroutine
        {
            public Coroutine(Plugin owner, IEnumerator instructions, Action onComplete, string id = null,
                int cachedMaxInstances = -1)
            {
                Owner = owner;
                OnComplete = onComplete;
                RecursiveInstructions = new LinkedList<IEnumerator>();
                RecursiveInstructions.AddFirst(instructions);
                CurrentInstructions = instructions;
                Id = id;
                CachedMaxInstances = cachedMaxInstances;
            }

            /// <summary>
            ///     The maximum allowed of this instance
            /// </summary>
            public int CachedMaxInstances { get; }

            /// <summary>
            ///     Returns if we should do counter cleanup for <see cref="Coroutines.OnFrame" />
            /// </summary>
            public bool HasCounter => CachedMaxInstances != -1;

            public string Id { get; }

            /// <summary>
            ///     The owner of this coroutine
            /// </summary>
            public Plugin Owner { get; }

            /// <summary>
            ///     The recursive list of instructions
            /// </summary>
            private LinkedList<IEnumerator> RecursiveInstructions { get; }

            /// <summary>
            ///     The current set of instructions
            /// </summary>
            private IEnumerator CurrentInstructions { get; set; }

            /// <summary>
            ///     The current stage/index of <see cref="CurrentInstructions" />
            /// </summary>
            private int CurrentStage { get; set; } = -1;

            /// <summary>
            ///     The current level/index of <see cref="RecursiveInstructions" />
            /// </summary>
            private int CurrentLevel { get; set; }

            /// <summary>
            ///     A bool used to stop ticking
            /// </summary>
            public bool Stop { get; set; }

            /// <summary>
            ///     The callback for when this coroutine is complete
            /// </summary>
            private Action OnComplete { get; }

            /// <summary>
            ///     <para>
            ///         On tick, it checks if the current instruction is completed or null, if so, it continues on to the next
            ///         instruction
            ///     </para>
            ///     <para>
            ///         If there are no more instructions, we will mark this coroutine as finished and to be removed & call
            ///         <see cref="OnComplete" />
            ///     </para>
            /// </summary>
            /// <param name="deltaTime">The current deltaTime</param>
            public void Tick(float deltaTime)
            {
                if (Stop) return;
                if (CurrentInstructions == null) goto finish;
                var currentInstruction = CurrentInstructions.Current;
                var coroutineInstruction = currentInstruction as ICoroutineInstruction;
                if (!coroutineInstruction?.IsCompleted ?? false)
                {
                    coroutineInstruction.Tick(deltaTime);
                    return;
                }

                CurrentStage++;
                try
                {
                    RestartStopWatch(Instance._stopwatch);
                    Owner.TrackStart();
                    var ret = CurrentInstructions.MoveNext();
                    Owner.TrackEnd();
                    if (ret)
                    {
                        CheckStopwatch();
                        CheckForInstructionRecursion();
                        return;
                    }
                }
                catch (Exception exception)
                {
                    Instance.PrintError(
                        "An exception occurred whilst executing coroutine from {0} {1} at stage {2}, level {3}:\n{4}\n{5}",
                        Owner.Name, GetIdOrEmpty(), CurrentStage, CurrentLevel,
                        exception.Message, exception.StackTrace);
                    goto finish;
                }

                CurrentStage = 0;
                CurrentLevel++;
                if (UpdateCurrentInstructions()) return;
                finish:
                Stop = true;
                OnComplete?.Invoke();
            }

            private string GetIdOrEmpty()
            {
                return string.IsNullOrEmpty(Id) ? string.Empty : $"named {Id}";
            }

            /// <summary>
            ///     Check if the current instruction is another list of instructions, if so add it to
            ///     <see cref="RecursiveInstructions" />
            /// </summary>
            private void CheckForInstructionRecursion()
            {
                var enumeratorInstruction = CurrentInstructions.Current as IEnumerator;
                if (enumeratorInstruction == null)
                {
                    var coroutineInstruction = CurrentInstructions.Current as Coroutine;
                    enumeratorInstruction = coroutineInstruction?.CurrentInstructions;
                    if (enumeratorInstruction == null) return;
                }

                RecursiveInstructions.AddLast(enumeratorInstruction);
                CurrentInstructions = enumeratorInstruction;
            }

            /// <summary>
            ///     Check if the execution time of the coroutine is greater than the time specified in the config
            /// </summary>
            private void CheckStopwatch()
            {
                Instance._stopwatch.Stop();
                var elapsed = Instance._stopwatch.ElapsedMilliseconds;
                if (Instance._config.WarnTimeMilliseconds > 0 &&
                    elapsed < Instance._config.WarnTimeMilliseconds) return;
                var stackTrace = Instance._config.PrintStackTraceOnWarn
                    ? $"\n{new Exception().StackTrace}"
                    : string.Empty;
                Instance.PrintWarning("A coroutine from {0} {1} stage {2}, level {3} took {4}ms{5}", Owner.Name,
                    GetIdOrEmpty(),
                    CurrentStage, CurrentLevel, elapsed,
                    stackTrace);
            }

            /// <summary>
            ///     Update the current recursive instructions and set it null if there are none left
            /// </summary>
            /// <returns>True if we have anymore instructions</returns>
            private bool UpdateCurrentInstructions()
            {
                RecursiveInstructions.RemoveLast();
                CurrentInstructions = RecursiveInstructions.Last?.Value;
                return true;
            }
        }

        #region Coroutine Instructions

        /// <summary>
        ///     The class used for coroutine instructions like <see cref="WaitForSeconds" />
        /// </summary>
        public interface ICoroutineInstruction
        {
            /// <summary>
            ///     Returns if this current instruction is complete
            /// </summary>
            bool IsCompleted { get; }

            /// <summary>
            ///     Tick the current instruction
            /// </summary>
            /// <param name="deltaTime">The current delta time (scaled time)</param>
            void Tick(float deltaTime);
        }


        /// <summary>
        ///     A <see cref="ICoroutineInstruction" /> to wait for a certain amount of milliseconds in real or scaled time
        /// </summary>
        public class WaitForMilliseconds : ICoroutineInstruction
        {
            public WaitForMilliseconds(float time) : this(time, !Instance._config.UseScaledTime)
            {
            }

            public WaitForMilliseconds(float time, bool realTime)
            {
                Reset(time, realTime);
            }

            /// <summary>
            ///     Are we using real or delta/scaled time?
            ///     Warning, not all games use delta/scaled time
            /// </summary>
            private bool RealTime { get; set; }

            /// <summary>
            ///     Returns the date time
            /// </summary>
            private static DateTime DateTime => DateTime.UtcNow;

            /// <summary>
            ///     Returns the current time in seconds
            /// </summary>
            private static double Milliseconds => GetMilliseconds(DateTime);

            /// <summary>
            ///     Returns total seconds from <see cref="DateTime" />
            /// </summary>
            private static Func<DateTime, double> GetMilliseconds => dateTime => dateTime.TimeOfDay.TotalMilliseconds;

            /// <summary>
            ///     Returns the time until this instruction expires (completes)
            /// </summary>
            private double ExpiryTime { get; set; }

            /// <summary>
            ///     The seconds given
            /// </summary>
            protected float Time { get; private set; }

            /// <summary>
            ///     Returns <see langword="true" /> if <see cref="ExpiryTime" /> has expired
            /// </summary>
            public bool IsCompleted => RealTime ? ExpiryTime <= Milliseconds : ExpiryTime <= 0f;

            /// <summary>
            ///     Remove <see cref="deltaTime" /> as milliseconds from <see cref="Time" />
            /// </summary>
            /// <param name="deltaTime">Current delta time (scaled time)</param>
            public void Tick(float deltaTime)
            {
                if (RealTime) return;
                ExpiryTime -= SecondsToMilliseconds(deltaTime);
            }

            /// <summary>
            ///     Set <see cref="Time" /> if given a time and reset expiry time
            /// </summary>
            public void Reset(float time = -1f)
            {
                Reset(time, !Instance._config.UseScaledTime);
            }

            public void Reset(float time, bool realTime)
            {
                RealTime = realTime;
                if (time > 0f) Time = time;
                ExpiryTime = realTime ? GetMilliseconds.Invoke(DateTime.AddMilliseconds(Time)) : time;
            }
        }

        /// <summary>
        ///     A <see cref="ICoroutineInstruction" /> to wait for a certain amount of seconds in real or scaled time
        /// </summary>
        public class WaitForSeconds : WaitForMilliseconds
        {
            public WaitForSeconds(float time) : this(time, !Instance._config.UseScaledTime)
            {
            }

            public WaitForSeconds(float time, bool realTime) : base(SecondsToMilliseconds(time), realTime)
            {
            }

            public new void Reset(float time = -1f)
            {
                Reset(time, !Instance._config.UseScaledTime);
            }

            public new void Reset(float time, bool realTime)
            {
                base.Reset(time > 0f ? SecondsToMilliseconds(time) : Time, realTime);
            }
        }


        /// <summary>
        ///     A <see cref="ICoroutineInstruction" /> to wait for a given boolean
        /// </summary>
        public class WaitForBool : ICoroutineInstruction
        {
            public WaitForBool(Func<bool> predicate)
            {
                Predicate = predicate;
            }

            /// <summary>
            ///     The boolean we are waiting for
            /// </summary>
            private Func<bool> Predicate { get; }

            /// <summary>
            ///     Returns if <see cref="Predicate" /> is true
            /// </summary>
            public bool IsCompleted => Predicate.Invoke();

            public void Tick(float deltaTime)
            {
            }
        }

        #endregion

        #endregion

        #region Hooks

        /// <summary>
        ///     On initialized, set instance
        /// </summary>
        private void Init()
        {
            Instance = this;
        }

        /// <summary>
        ///     On unload, unset instance
        /// </summary>
        private void Unload()
        {
            Instance = null;
        }

        /// <summary>
        ///     Tick all of the active coroutines and cleanup any of them that are done
        /// </summary>
        /// <param name="deltaTime">The current delta/scaled time</param>
        private void OnFrame(float deltaTime = 0f)
        {
            if (_currentCoroutineCount == 0) return;
            TrackEnd(); //end track on coroutine ticks
            var currentPluginNode = _coroutines.First;
            while (currentPluginNode != null)
            {
                var entry = currentPluginNode.Value;
                var currentCoroutineNode = entry.Value.First;
                while (currentCoroutineNode != null)
                {
                    var coroutine = currentCoroutineNode.Value;
                    coroutine.Tick(deltaTime);
                    if (!coroutine.Stop) goto end;
                    if (coroutine.HasCounter)
                    {
                        Number counter;
                        if (_maxCoroutineCounter.TryGetValue(coroutine.Id, out counter) &&
                            (counter.Value += 1) >= counter.Max)
                            _maxCoroutineCounter.Remove(coroutine.Id);
                    }

                    _currentCoroutineCount--;
                    entry.Value.Remove(currentCoroutineNode);
                    end:
                    currentCoroutineNode = currentCoroutineNode.Next;
                }

                if (entry.Value.Count <= 0) _coroutines.Remove(currentPluginNode);
                currentPluginNode = currentPluginNode.Next;
            }

            TrackStart(); //continue track on cleanup
            if (!_config.CheckQueueOnFrame) return;
            CheckQueue();
        }

        /// <summary>
        ///     On unload of a plugin, stop all of it's coroutines
        /// </summary>
        /// <param name="plugin">Target plugin (owner)</param>
        private void OnPluginUnloaded(Plugin plugin)
        {
            StopCoroutines(plugin);
        }

        #endregion

        #region Hook Methods

        /// <summary>
        ///     Register a maximum amount of instances of an id
        /// </summary>
        /// <param name="id">Target id</param>
        /// <param name="max">Target maximum</param>
        [HookMethod("RegisterMax")]
        public void RegisterMax(string id, int max)
        {
            _idToMax[id] = max;
        }

        /// <summary>
        ///     Unregister a previously set maximum of instances from <see cref="RegisterMax" />
        /// </summary>
        /// <param name="id">Target id</param>
        /// <returns><see langword="true" /> if an item was removed from <see cref="_idToMax" /></returns>
        [HookMethod("UnregisterMax")]
        public bool UnregisterMax(string id)
        {
            return _idToMax.Remove(id);
        }

        /// <summary>
        ///     Start an asynchronous task that isn't repeated
        /// </summary>
        /// <param name="owner">Owner</param>
        /// <param name="task">The task to complete</param>
        /// <param name="initialDelay">Any initial delay</param>
        /// <param name="id">Id for this coroutine</param>
        /// <param name="onComplete">Callback for when the task is completed</param>
        /// <returns>A <see cref="Coroutine" /> to run</returns>
        [HookMethod("GetDelayedTask")]
        public Coroutine GetDelayedTask(Plugin owner, Action task, float initialDelay = 0f, string id = null,
            Action onComplete = null)
        {
            return CreateCoroutine(owner, GetInitialDelayCoroutine(initialDelay, task), id, onComplete);
        }

        /// <summary>
        ///     The same as <see cref="StartDelayedTask" /> but it repeats
        /// </summary>
        /// <param name="owner">Owner</param>
        /// <param name="continuePredicate">Predicate to keep repeating</param>
        /// <param name="interval">Interval between each repetition</param>
        /// <param name="initialDelay">Any initial delay</param>
        /// <param name="id">Id of this coroutine</param>
        /// <param name="onComplete">Callback when <see cref="continuePredicate" /> returns false (task is complete)</param>
        /// <returns>A <see cref="Coroutine" /> to run</returns>
        [HookMethod("GetAsynchronousRepeatingTask")]
        public Coroutine GetAsynchronousRepeatingTask(Plugin owner, Func<bool> continuePredicate, float interval,
            float initialDelay = 0f, string id = null, Action onComplete = null)
        {
            return CreateCoroutine(owner, GetRepeatingCoroutine(
                continuePredicate, interval, initialDelay), id, onComplete);
        }

        /// <summary>
        ///     Asynchronously loop through list (this loops through the whole list no stopping)
        /// </summary>
        /// <param name="owner">Owner</param>
        /// <param name="callback">The callback for each item in the list</param>
        /// <param name="list">Target list</param>
        /// <param name="interval">Interval between each repetition</param>
        /// <param name="startIndex">The index in the list to start at</param>
        /// <param name="reverse">If it just should loop in reverse</param>
        /// <param name="completePerTick">How many loops to complete each tick</param>
        /// <param name="initialDelay">Any initial delay</param>
        /// <param name="id">Id of this coroutine</param>
        /// <param name="onComplete">Callback when list is done looping</param>
        /// <typeparam name="T">Type parameter of <see cref="list" /></typeparam>
        /// <returns>A <see cref="Coroutine" /> to run</returns>
        public Coroutine LoopListAsynchronously<T>(Plugin owner, Action<T> callback, IList<T> list,
            float interval, int startIndex = -1, bool reverse = false, int completePerTick = 1, float initialDelay = 0f,
            string id = null, Action onComplete = null)
        {
            if (list.Count != 0)
                return LoopListAsynchronously(owner, obj =>
                {
                    callback.Invoke(obj);
                    return true;
                }, list, interval, startIndex, reverse, completePerTick, initialDelay, id, onComplete);
            onComplete?.Invoke();
            return null;
        }

        /// <summary>
        ///     Asynchronously loop through list
        /// </summary>
        /// <param name="owner">Owner</param>
        /// <param name="callback">The callback for each item in the list (return false to stop looping)</param>
        /// <param name="list">Target list</param>
        /// <param name="interval">Interval between each repetition</param>
        /// <param name="startIndex">The index in the list to start at</param>
        /// <param name="reverse">If it just should loop in reverse</param>
        /// <param name="completePerTick">How many loops to complete each tick</param>
        /// <param name="initialDelay">Any initial delay</param>
        /// <param name="id">Id of this coroutine</param>
        /// <param name="onComplete">Callback when list is done looping</param>
        /// <typeparam name="T">Type parameter of <see cref="list" /></typeparam>
        /// <returns>A <see cref="Coroutine" /> to run</returns>
        public Coroutine LoopListAsynchronously<T>(Plugin owner, Func<T, bool> callback, IList<T> list,
            float interval, int startIndex = -1, bool reverse = false, int completePerTick = 1,
            float initialDelay = 0f, string id = null, Action onComplete = null)
        {
            var index = startIndex == -1 ? reverse ? list.Count - 1 : 0 : startIndex;
            if (reverse && completePerTick > 0) completePerTick = -completePerTick;
            var increment = reverse ? -1 : 1;
            return GetAsynchronousRepeatingTask(owner, () =>
            {
                var max = index + completePerTick;
                for (var i = index; reverse ? i >= 0 : i < max && i < list.Count; i += increment)
                {
                    var obj = list[i];
                    if (callback.Invoke(obj)) continue;
                    return false;
                }

                index = max;
                return reverse ? index > 0 : index < list.Count;
            }, interval, initialDelay, id, onComplete);
        }

        /// <summary>
        ///     Asynchronously search through a list (check if value is in list)
        /// </summary>
        /// <param name="owner">Owner</param>
        /// <param name="target">Target value</param>
        /// <param name="callback">Callback on complete</param>
        /// <param name="list">Target list</param>
        /// <param name="interval">Interval between loop</param>
        /// <param name="startIndex">The index in the list to start at</param>
        /// <param name="reverse">If it just should loop in reverse</param>
        /// <param name="completePerTick">How many loops to complete each tick</param>
        /// <param name="initialDelay">Any initial delay</param>
        /// <param name="id">Id of this coroutine</param>
        /// <typeparam name="T">Type parameter of <see cref="list" /></typeparam>
        /// <returns>A <see cref="Coroutine" /> to run</returns>
        public Coroutine SearchListAsynchronously<T>(Plugin owner, T target, Action<bool> callback, IList<T> list,
            float interval, int startIndex = -1, bool reverse = false, int completePerTick = 1, float initialDelay = 0f,
            string id = null)
        {
            var found = false;
            return LoopListAsynchronously(owner, obj =>
            {
                if (!obj.Equals(target)) return true;
                found = true;
                callback.Invoke(true);
                return false;
            }, list, interval, startIndex, reverse, completePerTick, initialDelay, id, () =>
            {
                if (found) return;
                callback.Invoke(false);
            });
        }

        /// <summary>
        ///     Asynchronously find a value from it's corresponding key in a <see cref="IDictionary{TKey,TValue}" />
        /// </summary>
        /// <param name="owner">Owner</param>
        /// <param name="target">Target key (Key -> Value)</param>
        /// <param name="callback">Callback with value</param>
        /// <param name="dictionary">Target dictionary</param>
        /// <param name="interval">Interval between loop</param>
        /// <param name="completePerTick">How many loops to complete each tick</param>
        /// <param name="initialDelay">Any initial delay</param>
        /// <param name="id">Id of this coroutine</param>
        /// <typeparam name="TK">Key type</typeparam>
        /// <typeparam name="TV">Value type</typeparam>
        /// <returns>A <see cref="Coroutine" /> to run</returns>
        public Coroutine SearchDictionaryAsynchronously<TK, TV>(Plugin owner, TK target, Action<TV> callback,
            IDictionary<TK, TV> dictionary, float interval, int completePerTick = 1, float initialDelay = 0f,
            string id = null)
        {
            if (dictionary.Count != 0)
                return SearchAsynchronously(owner, target, dictionary, item => item.Count,
                    (index, item) => index < item.Count,
                    (index, givenTarget, item) =>
                    {
                        var element = item.ElementAt(index);
                        return !element.Key?.Equals(target) ?? false ? default(TV) : element.Value;
                    }, callback, interval, completePerTick, initialDelay, id);
            callback.Invoke(default(TV));
            return null;
        }

        /// <summary>
        ///     Method to be implemented (searching)
        /// </summary>
        /// <param name="owner">Owner</param>
        /// <param name="target">Target item</param>
        /// <param name="item">Object to search in</param>
        /// <param name="getSize">Get size method</param>
        /// <param name="keepSearchGoing">Keep search going method</param>
        /// <param name="handleSearchIndex">Handle search method</param>
        /// <param name="callback">Callback with found object</param>
        /// <param name="interval">Interval between each repetition</param>
        /// <param name="completePerTick">How many loops to complete each tick</param>
        /// <param name="initialDelay">Any initial delay</param>
        /// <param name="id">Id of this coroutine</param>
        /// <typeparam name="T">Type to search in</typeparam>
        /// <typeparam name="TK">Type of <see cref="target" /></typeparam>
        /// <typeparam name="TV">Type of <see cref="handleSearchIndex" /> return</typeparam>
        /// <returns>A <see cref="Coroutine" /> to run</returns>
        public Coroutine SearchAsynchronously<T, TK, TV>(Plugin owner, TK target, T item, Func<T, int> getSize,
            Func<int, T, bool> keepSearchGoing, Func<int, TK, T, TV> handleSearchIndex, Action<TV> callback,
            float interval,
            int completePerTick = 1, float initialDelay = 0f, string id = null)
        {
            var found = false;
            var index = 0;
            return GetAsynchronousRepeatingTask(owner, () =>
            {
                var max = index + completePerTick;
                for (var i = index; i < max && i < getSize.Invoke(item); i++)
                {
                    var value = handleSearchIndex.Invoke(i, target, item);
                    if (value == null) continue;
                    found = true;
                    callback.Invoke(value);
                    return false;
                }

                index = max;
                return keepSearchGoing.Invoke(index, item);
            }, interval, initialDelay, id, () =>
            {
                if (found) return;
                callback.Invoke(default(TV));
            });
        }

        /// <summary>
        ///     Find a player from the server asynchronously
        /// </summary>
        /// <param name="owner">Owner plugin</param>
        /// <param name="data">Data about the player (name/id)</param>
        /// <param name="callback">Callback with player or <see langword="null" /></param>
        /// <param name="includeName">Should we check for name?</param>
        /// <param name="ignoreCase">Should we check name ignore case?</param>
        /// <param name="interval">Interval between each repetition</param>
        /// <param name="completePerTick">How many loops to complete each tick</param>
        /// <param name="initialDelay">Any initial delay</param>
        /// <param name="reverse">Start in reverse?</param>
        /// <param name="startIndex">Start index</param>
        /// <param name="id">Id of this coroutine</param>
        /// <typeparam name="T"></typeparam>
        private void FindPlayerAsynchronously<T>(Plugin owner, string data, Action<T> callback, bool includeName = true,
            bool ignoreCase = false, float interval = 0.01f, int completePerTick = 15, float initialDelay = 0f,
            bool reverse = false, int startIndex = -1, string id = null) where T : class, IPlayer
        {
            var list = players.Connected.ToList();
            if (list.Count == 0)
            {
                callback(null);
                return;
            }

            var found = false;
            LoopListAsynchronously(owner, player =>
                {
                    if (!player.Id.Equals(data) && (!includeName || !player.Name.Equals(data,
                        ignoreCase ? StringComparison.InvariantCultureIgnoreCase : StringComparison.Ordinal)))
                        return false;
                    callback(player as T);
                    found = true;
                    return true;
                }, list, interval, startIndex, reverse, completePerTick,
                initialDelay, id,
                () =>
                {
                    if (found) return;
                    callback(null);
                });
        }

        /// <summary>
        ///     Get a <see cref="IEnumerable{T}"/> of coroutines with that id
        /// </summary>
        /// <param name="id">Target id</param>
        /// <returns><see cref="IEnumerable{T}"/> of coroutines</returns>
        private IEnumerable<Coroutine> GetCoroutinesById(string id)
        {
            return GetAllCoroutines()
                .Select(coroutines =>
                    coroutines.FirstOrDefault(coroutine => Equals(coroutine.Id, id)))
                .DefaultIfEmpty();
        }

        /// <summary>
        ///     Check if a coroutine is running
        /// </summary>
        /// <param name="id">Coroutine id</param>
        /// <returns><see langword="true" /> if coroutine is running</returns>
        public bool IsCoroutineRunning(string id)
        {
            var coroutine = GetCoroutinesById(id).FirstOrDefault();
            return coroutine != null && !coroutine.Stop;
        }
        
        /// <summary>
        ///     Stop a coroutine with a given id
        /// </summary>
        /// <param name="id">Given id</param>
        /// <returns>If a coroutine was stopped</returns>
        [HookMethod("StopCoroutine")]
        public bool StopCoroutine(string id)
        {
            return GetCoroutinesById(id)
                .All(StopCoroutine);
        }

        /// <summary>
        ///     Stop coroutines started by <see cref="owner" />
        /// </summary>
        /// <param name="owner">Target plugin</param>
        [HookMethod("StopCoroutines")]
        private void StopCoroutines(Plugin owner)
        {
            LinkedList<Coroutine> coroutines;
            if (!TryGetCoroutines(owner.Name, out coroutines)) return;
            StopCoroutines(coroutines);
        }

        #endregion

        #region Coroutines

        /// <summary>
        ///     Start checking the queue from <see cref="_queuedCoroutines" />
        /// </summary>
        private void StartCheckingQueue()
        {
            if (_config.CheckQueueOnFrame || _queuedCoroutines.Count == 0) return;
            timer.In(_config.QueueCheckTime, () =>
            {
                CheckQueue();
                StartCheckingQueue();
            });
        }

        /// <summary>
        ///     Check if we should remove any coroutines from the queue
        /// </summary>
        private void CheckQueue()
        {
            var count = _currentCoroutineCount;
            if (!CanTakeCoroutine(count)) return;
            var allowed = Math.Min(_config.MaxRoutines - count, _queuedCoroutines.Count - 1);
            RemoveFromQueue(allowed);
        }

        /// <summary>
        ///     Remove <see cref="Coroutine" /> from the queue & run it
        /// </summary>
        /// <param name="allowed">Amount of <see cref="Coroutine" /> to run</param>
        private void RemoveFromQueue(int allowed)
        {
            var currentNode = _queuedCoroutines.First;
            var i = -1;
            while (currentNode != null && i < allowed)
            {
                var coroutine = currentNode.Value;
                if (coroutine.HasCounter && !CanTakeCoroutine(coroutine.Id, coroutine.CachedMaxInstances)) goto end;

                i++;
                ForcefullyAddCoroutine(coroutine);
                _queuedCoroutines.Remove(currentNode);
                end:
                currentNode = currentNode.Next;
            }
        }

        /// <summary>
        ///     Are there too many coroutines?
        /// </summary>
        /// <param name="count">Current number (if applicable)</param>
        /// <returns>If we can add another coroutine</returns>
        private bool CanTakeCoroutine(int count = -1)
        {
            if (count == -1) count = _currentCoroutineCount;
            return count < _config.MaxRoutines;
        }

        /// <summary>
        ///     Return the corresponding maximum from <see cref="_idToMax" />
        /// </summary>
        /// <param name="id">Target id</param>
        /// <returns>The instance maximum if applicable</returns>
        private int GetMaximumInstances(string id)
        {
            int max;
            return !string.IsNullOrEmpty(id) && _idToMax.TryGetValue(id, out max) ? max : -1;
        }

        /// <summary>
        ///     Create a <see cref="Coroutine" />
        /// </summary>
        /// <param name="owner">Owner</param>
        /// <param name="enumerator">The instructions (leave null to create instructions</param>
        /// <param name="id">Id of this coroutine</param>
        /// <param name="onComplete">Callback when the task completes</param>
        /// <returns>A <see cref="Coroutine" /> to run</returns>
        [HookMethod("CreateCoroutine")]
        private Coroutine CreateCoroutine(Plugin owner, IEnumerator enumerator = null, string id = null,
            Action onComplete = null)
        {
            var instructions = enumerator;
            var max = GetMaximumInstances(id);
            var routine = new Coroutine(owner, instructions, onComplete, id, max);
            return routine;
        }

        /// <summary>
        ///     Start a <see cref="Coroutine" />
        /// </summary>
        /// <param name="coroutine">Target coroutine</param>
        /// <returns>
        ///     <see langword="true" /> if the coroutine was stored & <see langword="false" /> if the coroutine is
        ///     <see langword="null" /> or it was queued
        /// </returns>
        public bool StartCoroutine(Coroutine coroutine)
        {
            if (coroutine == null) return false; //just in case you don't check it
            if (!CanTakeCoroutine() || !CanTakeCoroutine(coroutine.Id, coroutine.CachedMaxInstances))
            {
                if (_queuedCoroutines.AddLast(coroutine).Previous == null) StartCheckingQueue(); //if we're the first element
                return false;
            }

            ForcefullyAddCoroutine(coroutine);
            return true;
        }

        /// <summary>
        ///     Are there too many coroutines with the id of <see cref="id" /> (max being <see cref="max" />)
        /// </summary>
        /// <param name="id">Target id</param>
        /// <param name="max">Target maximum</param>
        /// <returns>
        ///     <see langword="true" /> if the amount of active coroutines with the id <see cref="id" /> is less than
        ///     <see cref="max" />
        /// </returns>
        private bool CanTakeCoroutine(string id, int max)
        {
            if (max == -1 || string.IsNullOrEmpty(id)) return true;

            Number count;
            var check = false;
            if (!_maxCoroutineCounter.TryGetValue(id, out count))
            {
                _maxCoroutineCounter[id] = count = new Number(max);
                check = true;
            }

            var newValue = count.Value - 1;
            var ret = newValue > 0;
            if ((newValue == 0 || ret) && !check) count.Value = newValue;
            return ret;
        }

        /// <summary>
        ///     Forcefully add a coroutine
        /// </summary>
        /// <param name="coroutine">Target coroutine</param>
        private void ForcefullyAddCoroutine(Coroutine coroutine)
        {
            var ownerName = coroutine.Owner.Name;
            LinkedList<Coroutine> coroutines;
            if (!TryGetCoroutines(ownerName, out coroutines))
            {
                _coroutines.AddLast(new KeyValuePair<string, LinkedList<Coroutine>>(ownerName,
                    coroutines = new LinkedList<Coroutine>()));
                _currentCoroutineCount++;
            }

            coroutines.AddLast(coroutine);
        }

        /// <summary>
        ///     Stop a <see cref="IEnumerable{T}" /> of coroutines
        /// </summary>
        /// <param name="coroutines"><see cref="IEnumerable{T}" /> of coroutines</param>
        private static void StopCoroutines(IEnumerable<Coroutine> coroutines)
        {
            foreach (var coroutine in coroutines) StopCoroutine(coroutine);
        }

        /// <summary>
        ///     Stop an individual coroutine
        /// </summary>
        /// <param name="coroutine">Target coroutine</param>
        private static bool StopCoroutine(Coroutine coroutine)
        {
            if (coroutine == null) return false;
            return coroutine.Stop = true;
        }

        /// <summary>
        ///     Returns coroutine instructions that wait an initial delay before completing a task
        /// </summary>
        /// <param name="initialDelay">Time to wait</param>
        /// <param name="task">Task to complete</param>
        /// <param name="waitForSeconds">The initial wait object if it exists</param>
        /// <returns></returns>
        private static IEnumerator GetInitialDelayCoroutine(float initialDelay = 0f, Action task = null,
            WaitForSeconds waitForSeconds = null)
        {
            waitForSeconds = waitForSeconds ?? new WaitForSeconds(initialDelay);
            yield return waitForSeconds;
            task?.Invoke();
        }

        /// <summary>
        ///     Get the coroutine to start it
        /// </summary>
        /// <param name="continuePredicate">Returns if the task should keep repeating</param>
        /// <param name="interval">Interval between repetitions</param>
        /// <param name="initialDelay">Any initial delay</param>
        /// <returns>A <see cref="IEnumerator" /> for a coroutine</returns>
        private static IEnumerator GetRepeatingCoroutine(Func<bool> continuePredicate,
            float interval,
            float initialDelay = 0f)
        {
            var hasInitial = initialDelay > 0f;
            var wait = new WaitForSeconds(hasInitial ? initialDelay : interval);
            if (hasInitial)
            {
                yield return GetInitialDelayCoroutine(waitForSeconds: wait);
                wait.Reset(interval);
            }

            while (continuePredicate.Invoke())
            {
                yield return wait;
                wait.Reset();
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        ///     Get a list of all the coroutines
        /// </summary>
        /// <returns>All coroutines</returns>
        private IEnumerable<LinkedList<Coroutine>> GetAllCoroutines()
        {
            return _coroutines.Select(entry => entry.Value);
        }

        /// <summary>
        ///     Finds the corresponding <see cref="coroutines" /> from <see cref="owner" />
        /// </summary>
        /// <param name="owner">Target owner</param>
        /// <param name="coroutines">Output coroutines</param>
        /// <returns>If we found coroutines</returns>
        private bool TryGetCoroutines(string owner, out LinkedList<Coroutine> coroutines)
        {
            foreach (var entry in _coroutines.Where(entry => entry.Key.Equals(owner)))
            {
                coroutines = entry.Value;
                return true;
            }

            coroutines = null;
            return false;
        }

        /// <summary>
        ///     Restart a stopwatch (a feature in modern .NET)
        /// </summary>
        /// <param name="stopwatch">Target stopwatch</param>
        private static void RestartStopWatch(Stopwatch stopwatch)
        {
            stopwatch.Reset();
            stopwatch.Start();
        }

        /// <summary>
        ///     Change <see cref="time" /> to milliseconds
        /// </summary>
        /// <param name="time">Time in seconds</param>
        /// <returns><see cref="time" /> as milliseconds</returns>
        private static float SecondsToMilliseconds(float time)
        {
            return time * 1000f;
        }

        private class Number
        {
            public Number(int value)
            {
                Value = value;
                Max = value;
            }

            public int Max { get; }
            public int Value { get; set; }
        }

        #endregion

        #region Configuration

        private ConfigFile _config;

        private class ConfigFile
        {
            [JsonProperty("Max Routines (others will be stacked up in a queue)")]
            public int MaxRoutines { get; set; }

            [JsonProperty("Queue Check Time (Seconds)")]
            public float QueueCheckTime { get; set; }

            [JsonProperty("Check Queue on Frame?")]
            public bool CheckQueueOnFrame { get; set; }

            [JsonProperty("Execution Warn Time (Milliseconds)")]
            public long WarnTimeMilliseconds { get; set; }

            [JsonProperty("Print Stacktrace on Warn?")]
            public bool PrintStackTraceOnWarn { get; set; }

            [JsonProperty("Use scaled time for time calculations?")]
            public bool UseScaledTime { get; set; }

            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    MaxRoutines = 100,
                    QueueCheckTime = 1f,
                    WarnTimeMilliseconds = 150,
                    PrintStackTraceOnWarn = false,
                    CheckQueueOnFrame = false,
                    UseScaledTime = true
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<ConfigFile>();
            if (_config == null) LoadDefaultConfig();
        }

        protected override void LoadDefaultConfig()
        {
            _config = ConfigFile.DefaultConfig();
            PrintWarning("Default configuration has been loaded.");
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        #endregion
    }
}