using System;
using System.Collections;
using System.Collections.Generic;
using LongShadow.Core;

namespace LongShadow.Mission.Runtime
{
    /// <summary>
    /// Deterministic coroutine runner for the mission scripts (TECH_PLAN §7.11 "coroutine scripts"). Unlike Unity
    /// coroutines it is plain C#: the MissionDirector ticks it from <c>Update</c> with scaled and unscaled delta time, and
    /// the EditMode tests tick it with a fixed step against a fake world, so every beat runs the same code in both.
    /// <para>Yield values: <c>null</c> (next tick), a <see cref="Wait"/> (until satisfied, checked at once), an
    /// <see cref="IEnumerator"/> (nested, runs to completion), a <see cref="ScriptTask"/> (until it ends). Scaled tasks
    /// see <see cref="Time"/> (gameplay time: pause and inserts freeze it, R3); unscaled tasks see <see cref="UnscaledTime"/>.</para>
    /// </summary>
    public sealed class ScriptRunner
    {
        /// <summary>Upper bound of zero-time steps per task and tick (catches scripts that never yield).</summary>
        public const int MaxStepsPerTick = 512;

        private readonly List<ScriptTask> _tasks = new List<ScriptTask>(16);
        private readonly List<ScriptTask> _scratch = new List<ScriptTask>(16);
        private int _nextId = 1;
        private bool _ticking;

        /// <summary>Gameplay seconds since the runner was created (sum of scaled deltas).</summary>
        public float Time { get; private set; }
        /// <summary>Real seconds since the runner was created.</summary>
        public float UnscaledTime { get; private set; }
        /// <summary>Ticks so far.</summary>
        public int Frame { get; private set; }

        /// <summary>Live tasks (oldest first).</summary>
        public IReadOnlyList<ScriptTask> Tasks => _tasks;

        /// <summary>Raised when a task throws (the task ends; the exception is also logged).</summary>
        public event Action<ScriptTask, Exception> Faulted;

        /// <summary>Starts a coroutine. It takes its first steps on the next <see cref="Tick"/>.</summary>
        public ScriptTask Start(IEnumerator routine, string name, ScriptTask owner = null, bool unscaled = false)
        {
            if (routine == null) throw new ArgumentNullException(nameof(routine));
            var t = new ScriptTask(_nextId++, name, owner, unscaled, routine) { StartedAt = unscaled ? UnscaledTime : Time };
            _tasks.Add(t);
            return t;
        }

        /// <summary>Stops a task and every task it owns.</summary>
        public void Stop(ScriptTask task)
        {
            if (task == null) return;
            for (var i = 0; i < _tasks.Count; i++)
            {
                var t = _tasks[i];
                if (!t.IsDone && t.IsOwnedBy(task)) End(t, true);
            }
            if (!task.IsDone) End(task, true);
        }

        /// <summary>Stops every task.</summary>
        public void StopAll()
        {
            for (var i = 0; i < _tasks.Count; i++)
                if (!_tasks[i].IsDone) End(_tasks[i], true);
        }

        /// <summary>Seconds on a task's clock.</summary>
        public float Now(bool unscaled) => unscaled ? UnscaledTime : Time;

        /// <summary>Advances the clocks and steps every live task until it blocks.</summary>
        public void Tick(float deltaTime, float unscaledDeltaTime)
        {
            if (_ticking) return;
            _ticking = true;
            try
            {
                Time += Math.Max(0f, deltaTime);
                UnscaledTime += Math.Max(0f, unscaledDeltaTime);
                Frame++;
                _scratch.Clear();
                _scratch.AddRange(_tasks);
                for (var i = 0; i < _scratch.Count; i++)
                {
                    var t = _scratch[i];
                    if (t.IsDone) continue;
                    if (!t.Unscaled && deltaTime <= 0f && t.CurrentWait != null && t.CurrentWait.Kind == WaitKind.Seconds) continue;
                    Step(t);
                }
                for (var i = _tasks.Count - 1; i >= 0; i--)
                    if (_tasks[i].IsDone) _tasks.RemoveAt(i);
            }
            finally
            {
                _ticking = false;
            }
        }

        private void Step(ScriptTask t)
        {
            for (var steps = 0; steps < MaxStepsPerTick; steps++)
            {
                if (t.IsDone) return;
                if (t.CurrentWait != null)
                {
                    bool done;
                    try
                    {
                        done = t.CurrentWait.IsDone;
                    }
                    catch (Exception e)
                    {
                        Fault(t, e);
                        return;
                    }
                    if (!done) return;
                    t.CurrentWait = null;
                }
                if (t.WaitingFor != null)
                {
                    if (!t.WaitingFor.IsDone) return;
                    t.WaitingFor = null;
                }
                if (t.Stack.Count == 0)
                {
                    End(t, false);
                    return;
                }
                var top = t.Stack.Peek();
                bool moved;
                try
                {
                    moved = top.MoveNext();
                }
                catch (Exception e)
                {
                    Fault(t, e);
                    return;
                }
                if (!moved)
                {
                    t.Stack.Pop();
                    if (t.Stack.Count == 0)
                    {
                        End(t, false);
                        return;
                    }
                    continue;
                }
                var y = top.Current;
                switch (y)
                {
                    case null:
                        return;
                    case Wait w:
                        t.CurrentWait = w;
                        continue;
                    case IEnumerator nested:
                        t.Stack.Push(nested);
                        continue;
                    case ScriptTask other:
                        if (ReferenceEquals(other, t))
                        {
                            LSLog.Error(LogCategory.Mission, "Script '" + t.Name + "' waits for itself; ignored.");
                            continue;
                        }
                        t.WaitingFor = other;
                        continue;
                    default:
                        LSLog.WarnOnce("runner-yield:" + y.GetType().Name, LogCategory.Mission,
                            "Script '" + t.Name + "' yielded an unsupported " + y.GetType().Name + "; treated as one tick.");
                        return;
                }
            }
            LSLog.WarnOnce("runner-steps:" + t.Name, LogCategory.Mission,
                "Script '" + t.Name + "' took " + MaxStepsPerTick + " zero-time steps in one tick; continuing next tick.");
        }

        private void Fault(ScriptTask t, Exception e)
        {
            t.Error = e;
            LSLog.Exception(LogCategory.Mission, e);
            LSLog.Error(LogCategory.Mission, "Script '" + t.Name + "' faulted and stopped.");
            End(t, false);
            Faulted?.Invoke(t, e);
        }

        private static void End(ScriptTask t, bool stopped)
        {
            if (t.IsDone) return;
            t.IsDone = true;
            t.WasStopped = stopped;
            t.CurrentWait = null;
            t.WaitingFor = null;
            while (t.Stack.Count > 0)
            {
                var e = t.Stack.Pop();
                if (e is IDisposable d)
                {
                    try
                    {
                        d.Dispose();
                    }
                    catch (Exception ex)
                    {
                        LSLog.Exception(LogCategory.Mission, ex);
                    }
                }
            }
        }
    }
}
