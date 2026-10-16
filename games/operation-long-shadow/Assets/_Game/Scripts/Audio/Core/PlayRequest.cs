using UnityEngine;

namespace LongShadow.Audio.Core
{
    /// <summary>Everything one pooled voice needs to start (built by the cue and weapon players).</summary>
    internal struct PlayRequest
    {
        public AudioClip Clip;
        public string CueId;
        public MixCategory Category;
        public Vector3 Position;
        public Transform Follow;
        public float Volume;
        public float Pitch;
        public float SpatialBlend;
        public float MinDistance;
        public float MaxDistance;
        public AudioRolloffMode Rolloff;
        public float Spread;
        public int Priority;
        /// <summary>Seconds before the sound starts (propagation delay).</summary>
        public float Delay;
        /// <summary>Seconds into the clip to start at (release tails).</summary>
        public float StartOffset;
        /// <summary>Loop the clip on the source (tinnitus longer than its clip).</summary>
        public bool LoopSource;
        /// <summary>Estimated loudness at the listener for stealing.</summary>
        public float Audibility;
    }
}
