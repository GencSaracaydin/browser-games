using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Audio.VO
{
    /// <summary>A queued or playing line request of the <see cref="VOManager"/>.</summary>
    public sealed class VORequest
    {
        /// <summary>Handle returned by <c>IVOService.Play</c>.</summary>
        public int Handle;
        /// <summary>The line.</summary>
        public DialogueLine Line;
        /// <summary>Caller options.</summary>
        public VOOptions Options;
        /// <summary>Effective priority.</summary>
        public VOPriority Priority;
        /// <summary>Channel the request plays on.</summary>
        public VOChannel Channel;
        /// <summary>Enqueue time (unscaled seconds).</summary>
        public float EnqueuedAt;
        /// <summary>Earliest start (enqueue + delay).</summary>
        public float EligibleAt;
        /// <summary>Dropped when not started by then (callouts go stale).</summary>
        public float ExpiresAt = float.MaxValue;
        /// <summary>FIFO order within a priority.</summary>
        public long Sequence;
        /// <summary>Jumps the queue within its priority (interrupting requests).</summary>
        public bool Urgent;
        /// <summary>Bark speaker or explicit speaker.</summary>
        public Transform Speaker;
        /// <summary>Bark team.</summary>
        public Team Team;
        /// <summary>When the clip data was requested (load wait).</summary>
        public float LoadRequestedAt = -1f;

        /// <summary>Line id (or null).</summary>
        public string LineId => Line != null ? Line.Id : null;
    }
}
