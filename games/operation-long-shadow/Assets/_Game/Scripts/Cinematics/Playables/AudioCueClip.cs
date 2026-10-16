using LongShadow.Cinematics.Booth;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LongShadow.Cinematics.Playables
{
    /// <summary>
    /// A sound at its clip start (CINEMATIC_DIRECTION §1.1 "audio leads picture": J-cuts start a shot's sound before its
    /// cut): 2D or positional one-shots (pen taps behind the left shoulder), loops that run for the clip and fade out
    /// at its end (tube buzz, rotor, cabin bed), sounds attached to a moving binding (rotor on the helicopter), music cues,
    /// stingers and music stops. Played through WP06's services; missing cues are skipped.
    /// </summary>
    [System.Serializable]
    public sealed class AudioCueClip : PlayableAsset, ITimelineClipAsset
    {
        /// <summary>How the cue plays.</summary>
        public enum Mode
        {
            /// <summary>2D one-shot.</summary>
            OneShot2D,
            /// <summary>3D one-shot at a binding/marker or a booth-local point.</summary>
            OneShotAt,
            /// <summary>Loop at a binding (or the booth point) for the clip, faded out at its end.</summary>
            Loop,
            /// <summary>One-shot attached to a moving binding, stopped at the clip end.</summary>
            Attached,
            /// <summary>Music cue (IMusicService.PlayCue).</summary>
            Music,
            /// <summary>Music stop with a fade.</summary>
            MusicStop,
            /// <summary>Stinger (IMusicService.Stinger).</summary>
            Stinger,
            /// <summary>Music layer change.</summary>
            MusicLayer,
        }

        [SerializeField] private Mode _mode = Mode.OneShot2D;
        [SerializeField] private string _cueId;
        [SerializeField, Tooltip("Binding or marker id for positional modes (empty = booth-local point).")] private string _at;
        [SerializeField] private Vector3 _boothPoint;
        [SerializeField] private float _volume = 1f;
        [SerializeField] private float _fadeOut = 0.5f;
        [SerializeField] private int _layer;

        /// <inheritdoc/>
        public ClipCaps clipCaps => ClipCaps.None;
        /// <summary>Playback mode.</summary>
        public Mode PlayMode { get => _mode; set => _mode = value; }
        /// <summary>Cue id (ASSET_MANIFEST id).</summary>
        public string CueId { get => _cueId; set => _cueId = value; }
        /// <summary>Position binding id.</summary>
        public string At { get => _at; set => _at = value; }
        /// <summary>Booth-local point when <see cref="At"/> is empty.</summary>
        public Vector3 BoothPoint { get => _boothPoint; set => _boothPoint = value; }
        /// <summary>Volume.</summary>
        public float Volume { get => _volume; set => _volume = value; }
        /// <summary>Fade-out at the clip end (loops, music stop).</summary>
        public float FadeOut { get => _fadeOut; set => _fadeOut = value; }
        /// <summary>Music layer (Music, MusicLayer).</summary>
        public int Layer { get => _layer; set => _layer = value; }

        /// <inheritdoc/>
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var p = ScriptPlayable<AudioCueBehaviour>.Create(graph);
            p.GetBehaviour().Clip = this;
            return p;
        }

        /// <summary>Starts the cue; returns a loop handle (0 for one-shots).</summary>
        public int Start()
        {
            switch (_mode)
            {
                case Mode.OneShot2D:
                    CinematicAudio.Play2D(_cueId, _volume);
                    return 0;
                case Mode.OneShotAt:
                    CinematicAudio.Play(_cueId, Position(), _volume);
                    return 0;
                case Mode.Loop:
                    return CinematicAudio.Loop(_cueId, Anchor());
                case Mode.Attached:
                    return CinematicAudio.Attached(_cueId, Anchor(), _volume);
                case Mode.Music:
                    CinematicAudio.Music?.PlayCue(_cueId, _layer, Mathf.Max(0.01f, _fadeOut));
                    return 0;
                case Mode.MusicStop:
                    CinematicAudio.Music?.Stop(_fadeOut);
                    return 0;
                case Mode.Stinger:
                    CinematicAudio.Stinger(_cueId);
                    return 0;
                case Mode.MusicLayer:
                    CinematicAudio.Music?.SetLayer(_layer, Mathf.Max(0.01f, _fadeOut));
                    return 0;
            }
            return 0;
        }

        private Vector3 Position()
        {
            var t = string.IsNullOrEmpty(_at) ? null : CinematicBindings.ResolveTransform(_at);
            if (t != null) return t.position;
            var booth = BoothRoot.Current;
            return booth != null ? booth.ToWorld(_boothPoint) : BoothLayout.WorldOrigin + _boothPoint;
        }

        private Transform Anchor()
        {
            var t = string.IsNullOrEmpty(_at) ? null : CinematicBindings.ResolveTransform(_at);
            if (t != null) return t;
            var booth = BoothRoot.Current;
            return booth != null ? booth.transform : null;
        }
    }

    /// <summary>Starts a cue on clip entry and stops loops on exit.</summary>
    internal sealed class AudioCueBehaviour : PlayableBehaviour
    {
        public AudioCueClip Clip;
        private int _handle;

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            if (Clip == null || !PlayableUtil.IsLive(info)) return;
            _handle = Clip.Start();
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            if (_handle == 0) return;
            CinematicAudio.Stop(_handle, Clip != null ? Clip.FadeOut : 0.3f);
            _handle = 0;
        }
    }
}
