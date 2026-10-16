using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>Optional parameters for <see cref="IVOService.Play"/>. <c>default</c> means "use the line's data".</summary>
    public struct VOOptions
    {
        /// <summary>Speaker transform for the dry 3D source (null = resolve from the line's speaker id).</summary>
        public Transform Speaker;
        /// <summary>Seconds to wait before starting (unscaled).</summary>
        public float Delay;
        /// <summary>Interrupt the current net line instead of queueing.</summary>
        public bool Interrupt;
        /// <summary>Force the radio chain regardless of distance.</summary>
        public bool ForceRadio;
        /// <summary>Force the dry chain regardless of distance.</summary>
        public bool ForceDry;
        /// <summary>Suppress the subtitle.</summary>
        public bool NoSubtitle;
        /// <summary>Volume multiplier (0 = default 1).</summary>
        public float Volume;
        /// <summary>Override the line's priority (null = data).</summary>
        public VOPriority? Priority;
        /// <summary>
        /// G9 positional radio: with <see cref="ForceDry"/> the line starts in 3D at <see cref="Speaker"/> and crossfades
        /// onto the 2D radio chain this many seconds into the line (D039 at "Seven"). 0 = no switch.
        /// </summary>
        public float RadioSwitchTime;
    }
}
