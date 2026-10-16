using System;
using UnityEngine;

namespace LongShadow.Audio.VO
{
    /// <summary>
    /// One processed take of a line in one VO chain (<c>VO_&lt;LineId&gt;_&lt;CHAIN&gt;[_A|_B].wav</c>, TECH_PLAN §4.2) with its
    /// 30 Hz amplitude envelope (TECH_PLAN D10: <c>AudioSource.GetOutputData</c> does not exist on the Web, so the VU
    /// needle, jaw flap and look-at read precomputed data). The envelope is stored as bytes normalised to the clip's
    /// own peak RMS (0..255 = 0..1).
    /// </summary>
    [Serializable]
    public sealed class DialogueClip
    {
        [SerializeField] private string _chain;
        [SerializeField, Tooltip("\"\" or the bark voice \"A\" / \"B\".")] private string _variant = "";
        [SerializeField] private AudioClip _clip;
        [SerializeField] private float _duration;
        [SerializeField, Tooltip("Seconds from the clip start to the first word (TAPE adds a motor start).")]
        private float _speechOffset;
        [SerializeField] private byte[] _envelope = new byte[0];

        /// <summary>Serialization constructor.</summary>
        public DialogueClip()
        {
        }

        /// <summary>Creates a clip entry (importer and tests).</summary>
        public DialogueClip(string chain, string variant, AudioClip clip, float duration, float speechOffset, byte[] envelope)
        {
            _chain = chain;
            _variant = variant ?? "";
            _clip = clip;
            _duration = duration > 0f ? duration : clip != null ? clip.length : 0f;
            _speechOffset = Mathf.Max(0f, speechOffset);
            _envelope = envelope ?? new byte[0];
        }

        /// <summary>Chain name (<see cref="VOChains"/>).</summary>
        public string Chain => _chain;
        /// <summary>"" or "A"/"B" (Legion bark voices).</summary>
        public string Variant => _variant ?? "";
        /// <summary>The clip (null while the file has not landed).</summary>
        public AudioClip Clip => _clip;
        /// <summary>Length in seconds (from the envelope file or the clip).</summary>
        public float Duration => _clip != null ? _clip.length : _duration;
        /// <summary>Seconds before the first word.</summary>
        public float SpeechOffset => _speechOffset;
        /// <summary>30 Hz envelope bytes (0..255 = 0..1 of the clip's peak RMS).</summary>
        public byte[] Envelope => _envelope;
    }
}
