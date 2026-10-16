using LongShadow.Core;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LongShadow.Cinematics.Playables
{
    /// <summary>
    /// A dialogue line at its clip start (TECH_PLAN §7.12 VOLineClip), played through WP06's IVOService (chains, ducking,
    /// subtitles, the scrambler's VU envelope). Scrub-safe: nothing plays outside play-mode playback. When the VO
    /// service or the line is missing the clip raises the subtitle itself from the DIALOGUE.json text the builder stored,
    /// for the clip's length, so the story still reads.
    /// </summary>
    [System.Serializable]
    public sealed class VOLineClip : PlayableAsset, ITimelineClipAsset
    {
        private static int s_handle = 900000;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_handle = 900000;

        [SerializeField] private string _lineId;
        [SerializeField] private string _speakerId;
        [SerializeField, Tooltip("Binding id of the speaking body for the dry 3D source (empty = the line's default).")]
        private string _speakerBinding;
        [SerializeField] private bool _forceRadio;
        [SerializeField] private bool _forceDry;
        [SerializeField] private bool _interrupt = true;
        [SerializeField] private float _volume;
        [SerializeField, TextArea(1, 3)] private string _subtitle;
        [SerializeField] private string _callsign;

        /// <inheritdoc/>
        public ClipCaps clipCaps => ClipCaps.None;
        /// <summary>DIALOGUE.json line id.</summary>
        public string LineId { get => _lineId; set => _lineId = value; }
        /// <summary>Speaker id (DIALOGUE.json speaker).</summary>
        public string SpeakerId { get => _speakerId; set => _speakerId = value; }
        /// <summary>Binding id of the speaker transform.</summary>
        public string SpeakerBinding { get => _speakerBinding; set => _speakerBinding = value; }
        /// <summary>Force the radio chain.</summary>
        public bool ForceRadio { get => _forceRadio; set => _forceRadio = value; }
        /// <summary>Force the dry chain.</summary>
        public bool ForceDry { get => _forceDry; set => _forceDry = value; }
        /// <summary>Interrupt the current net line.</summary>
        public bool Interrupt { get => _interrupt; set => _interrupt = value; }
        /// <summary>Volume multiplier (0 = default).</summary>
        public float Volume { get => _volume; set => _volume = value; }
        /// <summary>Fallback subtitle text (DIALOGUE.json subtitle/text).</summary>
        public string Subtitle { get => _subtitle; set => _subtitle = value; }
        /// <summary>Fallback callsign.</summary>
        public string Callsign { get => _callsign; set => _callsign = value; }

        /// <inheritdoc/>
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var p = ScriptPlayable<VOLineBehaviour>.Create(graph);
            p.GetBehaviour().Clip = this;
            return p;
        }

        /// <summary>Plays the line (or the fallback subtitle); returns the subtitle handle of the fallback (0 otherwise).</summary>
        public int Play(float clipSeconds)
        {
            var vo = CinematicAudio.VO;
            if (vo != null && vo.HasLine(_lineId))
            {
                var o = new VOOptions
                {
                    Speaker = string.IsNullOrEmpty(_speakerBinding) ? null : CinematicBindings.ResolveTransform(_speakerBinding),
                    Interrupt = _interrupt,
                    ForceRadio = _forceRadio,
                    ForceDry = _forceDry,
                    Volume = _volume,
                    Priority = VOPriority.Scripted,
                };
                vo.Play(_lineId, o);
                return 0;
            }
            LSLog.WarnOnce("cine-vo:" + _lineId, LogCategory.Cinematics, "Line " + _lineId + " has no VO; showing its subtitle only.");
            if (string.IsNullOrEmpty(_subtitle)) return 0;
            var style = SpeakerIds.StyleOf(_speakerId);
            var handle = ++s_handle;
            GameEvents.RaiseSubtitleShow(new SubtitleData
            {
                Handle = handle,
                LineId = _lineId,
                Speaker = string.IsNullOrEmpty(style.DisplayName) ? _speakerId : style.DisplayName,
                Callsign = _callsign,
                Text = _subtitle,
                Color = style.Color,
                Radio = _forceRadio,
                Italic = style.ItalicInBooth,
                Monospace = style.Monospace,
                IsBark = false,
                Duration = clipSeconds,
            });
            return handle;
        }
    }

    /// <summary>Starts the line when its clip starts during playback; hides a fallback subtitle at the clip end.</summary>
    internal sealed class VOLineBehaviour : PlayableBehaviour
    {
        public VOLineClip Clip;
        private int _subtitle;

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            if (Clip == null || !PlayableUtil.IsLive(info)) return;
            _subtitle = Clip.Play((float)playable.GetDuration());
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            if (_subtitle == 0) return;
            GameEvents.RaiseSubtitleHide(_subtitle);
            _subtitle = 0;
        }
    }
}
