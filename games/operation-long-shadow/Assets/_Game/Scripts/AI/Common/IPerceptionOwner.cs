using LongShadow.Core;
using UnityEngine;

namespace LongShadow.AI.Common
{
    /// <summary>The agent a <see cref="Perception"/> sees and hears for (enemy brain, squad member).</summary>
    public interface IPerceptionOwner
    {
        /// <summary>Team of the perceiver (hostility).</summary>
        Team Team { get; }
        /// <summary>Eye position for line-of-sight rays (changes while peeking).</summary>
        Vector3 EyePosition { get; }
        /// <summary>Facing on the XZ plane (field of view).</summary>
        Vector3 Forward { get; }
        /// <summary>Fixed sight range in metres (marksman 160); 0 = LightStage range.</summary>
        float FixedSightRange { get; }
        /// <summary>True when already fighting: new targets are detected almost at once.</summary>
        bool InCombat { get; }
        /// <summary>False while perception is suspended (dead, scripted, hidden).</summary>
        bool PerceptionEnabled { get; }
        /// <summary>
        /// Attention limits while unaware (Beat 2 smoking sentries, see <see cref="SentryAttention"/>): sight range cap in
        /// metres (0 = none) and field of view in degrees (0 = the GDD §14.2 default).
        /// </summary>
        void AttentionLimits(out float rangeCap, out float fovDegrees);
        /// <summary>
        /// Multiplier on the detection time for a visible target (1 = GDD §14.2; larger = slower; 0 or less = this
        /// sighting adds nothing to the detection meter).
        /// </summary>
        float DetectionTimeScale(ITargetable target, float distance);
    }
}
