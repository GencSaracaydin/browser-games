using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LongShadow.Cinematics.Playables
{
    /// <summary>
    /// A full-screen ramp over the clip (TECH_PLAN §7.12 ScreenFXClip): fades to black or white, exposure white-outs
    /// (intro shot 9: +6 EV to 0 over 2 s), desaturation, glitch, the MID-B film burn-out, a feed cross-dissolve (I3),
    /// vignette, and the booth grade's desaturation (outro shot 5: −30 %). The value goes From → To with easing; the
    /// track's channel returns to neutral when no clip is active.
    /// </summary>
    [System.Serializable]
    public sealed class ScreenFXClip : PlayableAsset, ITimelineClipAsset
    {
        [SerializeField] private float _from;
        [SerializeField] private float _to = 1f;
        [SerializeField] private ShotEase _ease = ShotEase.Linear;
        [SerializeField] private Color _color = Color.black;
        [SerializeField, Tooltip("Feed channel: feed id (Feed.Film ...).")] private string _feedId;
        [SerializeField, Tooltip("Glitch channel: white frame on entry.")] private bool _whiteFrame;

        /// <inheritdoc/>
        public ClipCaps clipCaps => ClipCaps.None;
        /// <summary>Value at the start.</summary>
        public float From { get => _from; set => _from = value; }
        /// <summary>Value at the end.</summary>
        public float To { get => _to; set => _to = value; }
        /// <summary>Ramp easing.</summary>
        public ShotEase Ease { get => _ease; set => _ease = value; }
        /// <summary>Fade colour.</summary>
        public Color Color { get => _color; set => _color = value; }
        /// <summary>Feed id (Feed channel).</summary>
        public string FeedId { get => _feedId; set => _feedId = value; }
        /// <summary>White frame on entry (Glitch channel).</summary>
        public bool WhiteFrame { get => _whiteFrame; set => _whiteFrame = value; }

        /// <summary>Value at normalised time.</summary>
        public float ValueAt(float t01) => Mathf.LerpUnclamped(_from, _to, Easing.Evaluate(_ease, t01));

        /// <inheritdoc/>
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var p = ScriptPlayable<ScreenFXBehaviour>.Create(graph);
            p.GetBehaviour().Clip = this;
            return p;
        }
    }

    /// <summary>Per-clip behaviour of a screen FX ramp.</summary>
    internal sealed class ScreenFXBehaviour : PlayableBehaviour
    {
        public ScreenFXClip Clip;
    }
}
