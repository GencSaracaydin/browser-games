using System;
using System.Collections;
using System.Collections.Generic;

namespace LongShadow.Mission.Runtime
{
    /// <summary>One coroutine run by a <see cref="ScriptRunner"/> (a beat script, a side task, a reload flow).</summary>
    public sealed class ScriptTask
    {
        internal readonly Stack<IEnumerator> Stack = new Stack<IEnumerator>(8);

        /// <summary>Name for logs and tools.</summary>
        public readonly string Name;
        /// <summary>Task that owns this one (stopping the owner stops it), or null.</summary>
        public readonly ScriptTask Owner;
        /// <summary>Runs on unscaled time (reload and death flows; insert freezes do not stop it).</summary>
        public readonly bool Unscaled;
        /// <summary>Unique id.</summary>
        public readonly int Id;

        /// <summary>The wait the task is blocked on, or null.</summary>
        public Wait CurrentWait { get; internal set; }
        /// <summary>The task this task waits for, or null.</summary>
        public ScriptTask WaitingFor { get; internal set; }
        /// <summary>True once finished, stopped or faulted.</summary>
        public bool IsDone { get; internal set; }
        /// <summary>True when stopped from outside.</summary>
        public bool WasStopped { get; internal set; }
        /// <summary>The exception that ended the task, or null.</summary>
        public Exception Error { get; internal set; }
        /// <summary>Runner time when the task started.</summary>
        public float StartedAt { get; internal set; }

        internal ScriptTask(int id, string name, ScriptTask owner, bool unscaled, IEnumerator routine)
        {
            Id = id;
            Name = name;
            Owner = owner;
            Unscaled = unscaled;
            Stack.Push(routine);
        }

        /// <summary>True if <paramref name="other"/> is this task or one of its owners.</summary>
        public bool IsOwnedBy(ScriptTask other)
        {
            for (var t = this; t != null; t = t.Owner)
                if (ReferenceEquals(t, other)) return true;
            return false;
        }

        /// <inheritdoc/>
        public override string ToString() => Name + (IsDone ? " [done]" : CurrentWait != null ? " waits " + CurrentWait : "");
    }
}
