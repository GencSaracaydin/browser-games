using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Audio.Editor
{
    /// <summary>Playback settings of one cue (<see cref="CueDefaults.Resolve"/>), written into its <c>AudioCue</c>.</summary>
    public struct CueSettings
    {
        /// <summary>Mix category (<see cref="AudioCategories"/>).</summary>
        public string Category;
        /// <summary>0 = 2D, 1 = 3D.</summary>
        public float SpatialBlend;
        /// <summary>Rolloff min distance (m).</summary>
        public float MinDistance;
        /// <summary>Rolloff max distance (m).</summary>
        public float MaxDistance;
        /// <summary>Rolloff mode.</summary>
        public AudioRolloffMode Rolloff;
        /// <summary>3D spread (degrees).</summary>
        public float Spread;
        /// <summary>Linear volume.</summary>
        public float Volume;
        /// <summary>± dB per play.</summary>
        public float VolumeJitterDb;
        /// <summary>± pitch fraction per play.</summary>
        public float PitchJitter;
        /// <summary>Stealing importance 0..100.</summary>
        public int Priority;
        /// <summary>Distance / speed-of-sound delay on 3D plays.</summary>
        public bool SpeedOfSound;
        /// <summary>Max simultaneous instances (0 = unlimited).</summary>
        public int MaxInstances;
        /// <summary>Minimum seconds between starts.</summary>
        public float MinInterval;
        /// <summary>Near → far crossfade distances (x near end, y far start).</summary>
        public Vector2 FarCrossfade;
        /// <summary>Rolloff min distance of the far variant.</summary>
        public float FarMinDistance;
        /// <summary>Rolloff max distance of the far variant.</summary>
        public float FarMaxDistance;

        /// <summary>The generic 3D effect defaults.</summary>
        public static CueSettings Effect3D(float min, float max, int priority) => new CueSettings
        {
            Category = AudioCategories.Effects, SpatialBlend = 1f, MinDistance = min, MaxDistance = max,
            Rolloff = AudioRolloffMode.Logarithmic, Volume = 1f, VolumeJitterDb = 1f, PitchJitter = 0.03f, Priority = priority,
            FarCrossfade = new Vector2(40f, 120f), FarMinDistance = 30f, FarMaxDistance = 1500f,
        };
    }
}
