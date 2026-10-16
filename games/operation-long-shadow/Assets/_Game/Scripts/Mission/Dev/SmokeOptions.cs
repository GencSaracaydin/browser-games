using System;
using LongShadow.Core;

namespace LongShadow.Mission.Dev
{
    /// <summary>Options of <see cref="MissionSmokeTest"/> (TECH_PLAN §10.3).</summary>
    [Serializable]
    public sealed class SmokeOptions
    {
        /// <summary>Checkpoints to visit, in order (default: CP1 … CP10 + CP3b).</summary>
        public string[] Checkpoints = (string[])SequenceIds.Checkpoints.Clone();
        /// <summary>Seconds with AI fighting at each checkpoint (TECH_PLAN §10.3: 8 s).</summary>
        public float DwellSeconds = 8f;
        /// <summary>Longest wait for a checkpoint's sections to load.</summary>
        public float SectionTimeout = 90f;
        /// <summary>Seconds after the jump before the asserts run.</summary>
        public float SettleSeconds = 2f;
        /// <summary>Screenshots to Builds/captures/smoke_&lt;ts&gt;/&lt;cp&gt;.png (editor and desktop only).</summary>
        public bool Screenshots = true;
        /// <summary>God mode during the run.</summary>
        public bool GodMode = true;
    }
}
