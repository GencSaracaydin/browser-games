using System;
using System.Collections.Generic;
using LongShadow.Audio.Core;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Audio.VO
{
    /// <summary>
    /// One line of <c>Docs/DIALOGUE.json</c> as the game plays it (TECH_PLAN §7.10): speaker, callsign, subtitle text,
    /// the clips of every chain its manifest entry lists, envelopes, beat window, priority, channel and the checkpoint
    /// rules (<see cref="ObjectiveCritical"/>, <see cref="Repeatable"/>). Built by <c>DialogueImporter</c> into
    /// <c>LIB_Dialogue</c>; every DIALOGUE line has an entry even before its audio lands (then <see cref="Duration"/> is an
    /// estimate from the word count and the line plays silently with its subtitle).
    /// </summary>
    [Serializable]
    public sealed class DialogueLine : ILibraryEntry
    {
        [SerializeField] private string _id;
        [SerializeField] private string _speaker;
        [SerializeField] private string _callsign;
        [SerializeField] private string _voiceId;
        [SerializeField, TextArea(1, 3)] private string _text;
        [SerializeField, TextArea(1, 3), Tooltip("DIALOGUE.json \"subtitle\" if present, else \"text\".")]
        private string _subtitle;
        [SerializeField] private bool _radioFilter;
        [SerializeField, Tooltip("Beat window (0 intro, 1-11, 12 outro, -1 always loaded).")]
        private int _beat = AudioConstants.AlwaysBeat;
        [SerializeField] private VOPriority _priority = VOPriority.Scripted;
        [SerializeField] private VOChannel _channel = VOChannel.Net;
        [SerializeField, Tooltip("Repeats after a checkpoint reload even if it played before the checkpoint (GDD §17).")]
        private bool _objectiveCritical;
        [SerializeField, Tooltip("Systemic line that may play any number of times (callouts, barks, death inserts, OOB).")]
        private bool _repeatable;
        [SerializeField] private float _duration;
        [SerializeField, Tooltip("G9: seconds into the line where a positional start jumps onto the 2D radio (D039 at \"Seven\").")]
        private float _radioSwitchTime;
        [SerializeField, Tooltip("Chains in ASSET_MANIFEST order; the first is the primary chain.")]
        private string[] _chains = new string[0];
        [SerializeField] private List<DialogueClip> _clips = new List<DialogueClip>();

        /// <summary>Serialization constructor.</summary>
        public DialogueLine()
        {
        }

        /// <summary>Creates a line (importer and tests).</summary>
        public DialogueLine(string id, string speaker, string callsign, string text, string subtitle, bool radioFilter)
        {
            _id = id;
            _speaker = speaker;
            _callsign = callsign;
            _text = text;
            _subtitle = string.IsNullOrEmpty(subtitle) ? text : subtitle;
            _radioFilter = radioFilter;
        }

        /// <inheritdoc />
        public string Id => _id;
        /// <summary>DIALOGUE.json speaker id (<see cref="SpeakerIds"/>).</summary>
        public string Speaker => _speaker;
        /// <summary>Callsign shown in brackets.</summary>
        public string Callsign => _callsign;
        /// <summary>Casting key (<c>vo_hollis</c>).</summary>
        public string VoiceId => _voiceId;
        /// <summary>Spoken text.</summary>
        public string Text => _text;
        /// <summary>Subtitle text (translation for barks).</summary>
        public string Subtitle => string.IsNullOrEmpty(_subtitle) ? _text : _subtitle;
        /// <summary>DIALOGUE.json <c>radio_filter</c>: always on the radio chain.</summary>
        public bool RadioFilter => _radioFilter;
        /// <summary>Beat window (-1 = always loaded).</summary>
        public int Beat => _beat;
        /// <summary>Default priority.</summary>
        public VOPriority Priority => _priority;
        /// <summary>Playback channel.</summary>
        public VOChannel Channel => _channel;
        /// <summary>GDD §17 objective-critical line.</summary>
        public bool ObjectiveCritical => _objectiveCritical;
        /// <summary>Systemic, repeatable line.</summary>
        public bool Repeatable => _repeatable;
        /// <summary>Seconds (primary clip, or the estimate while no clip exists).</summary>
        public float Duration
        {
            get
            {
                var c = PrimaryClip;
                return c != null && c.Duration > 0f ? c.Duration : _duration;
            }
        }
        /// <summary>Default G9 radio switch time (0 = none).</summary>
        public float RadioSwitchTime => _radioSwitchTime;
        /// <summary>Chains in manifest order.</summary>
        public string[] Chains => _chains;
        /// <summary>All clips.</summary>
        public IReadOnlyList<DialogueClip> Clips => _clips;
        /// <summary>True for an enemy bark.</summary>
        public bool IsBark => _channel == VOChannel.Bark;

        /// <summary>True if at least one clip has audio.</summary>
        public bool HasAudio
        {
            get
            {
                for (var i = 0; i < _clips.Count; i++)
                    if (_clips[i] != null && _clips[i].Clip != null) return true;
                return false;
            }
        }

        /// <summary>The first chain of the manifest that has a clip (any variant), else the first clip, else null.</summary>
        public DialogueClip PrimaryClip
        {
            get
            {
                if (_chains != null)
                {
                    for (var i = 0; i < _chains.Length; i++)
                    {
                        var c = Find(_chains[i], null);
                        if (c != null && c.Clip != null) return c;
                    }
                }
                for (var i = 0; i < _clips.Count; i++)
                    if (_clips[i] != null && _clips[i].Clip != null) return _clips[i];
                return _clips.Count > 0 ? _clips[0] : null;
            }
        }

        /// <summary>The primary chain name (or null).</summary>
        public string PrimaryChain
        {
            get
            {
                var c = PrimaryClip;
                if (c != null) return c.Chain;
                return _chains != null && _chains.Length > 0 ? _chains[0] : null;
            }
        }

        /// <summary>
        /// The clip of a chain. <paramref name="variant"/> "A"/"B" picks a bark voice (falling back to the other voice or
        /// the unsuffixed clip); null takes the first match. Clips without audio are returned too (callers check
        /// <see cref="DialogueClip.Clip"/>).
        /// </summary>
        public DialogueClip Find(string chain, string variant)
        {
            DialogueClip any = null;
            for (var i = 0; i < _clips.Count; i++)
            {
                var c = _clips[i];
                if (c == null || c.Chain != chain) continue;
                if (variant == null || c.Variant == variant) return c;
                if (any == null || (any.Clip == null && c.Clip != null)) any = c;
            }
            return any;
        }

        /// <summary>True when a chain has a playable clip.</summary>
        public bool HasChain(string chain)
        {
            var c = Find(chain, null);
            return c != null && c.Clip != null;
        }

        /// <summary>Sets the classification (importer and tests). Returns this.</summary>
        public DialogueLine WithRules(int beat, VOPriority priority, VOChannel channel, bool objectiveCritical, bool repeatable)
        {
            _beat = beat;
            _priority = priority;
            _channel = channel;
            _objectiveCritical = objectiveCritical;
            _repeatable = repeatable;
            return this;
        }

        /// <summary>Sets audio data (importer and tests). Returns this.</summary>
        public DialogueLine WithAudio(string voiceId, string[] chains, IEnumerable<DialogueClip> clips, float estimatedDuration, float radioSwitchTime)
        {
            _voiceId = voiceId;
            _chains = chains ?? new string[0];
            _clips.Clear();
            if (clips != null) _clips.AddRange(clips);
            _duration = Mathf.Max(0f, estimatedDuration);
            _radioSwitchTime = Mathf.Max(0f, radioSwitchTime);
            return this;
        }
    }
}
