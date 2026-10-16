using LongShadow.Cinematics.Booth;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LongShadow.Cinematics.Playables
{
    /// <summary>
    /// Fluorescent tube behaviour from the clip start (TECH_PLAN §7.12 LightFlickerClip): off, stutter on with N blinks
    /// (intro shot 2), steady, unstable, dying (outro shot 10).
    /// </summary>
    [System.Serializable]
    public sealed class LightFlickerClip : PlayableAsset, ITimelineClipAsset
    {
        [SerializeField] private FlickerMode _mode = FlickerMode.On;
        [SerializeField, Range(1, 4)] private int _blinks = 4;

        /// <inheritdoc/>
        public ClipCaps clipCaps => ClipCaps.None;
        /// <summary>Tube mode.</summary>
        public FlickerMode Mode { get => _mode; set => _mode = value; }
        /// <summary>Blinks of a stutter-on.</summary>
        public int Blinks { get => _blinks; set => _blinks = value; }

        /// <inheritdoc/>
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var p = ScriptPlayable<LightFlickerBehaviour>.Create(graph);
            p.GetBehaviour().Clip = this;
            return p;
        }
    }

    /// <summary>Per-clip behaviour of a tube clip (the track's mixer applies it on entry).</summary>
    internal sealed class LightFlickerBehaviour : PlayableBehaviour
    {
        public LightFlickerClip Clip;
    }
}
