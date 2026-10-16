using UnityEngine;

namespace LongShadow.Core.Editor
{
    /// <summary>Resolved AudioImporter settings for one clip (output of <see cref="AudioRules.Resolve"/>).</summary>
    public sealed class AudioRule
    {
        /// <summary>Rule id ("SFX/Weapons", "VO/RADIO", "Music").</summary>
        public string Name = "";
        /// <summary>Force to mono (normalisation is always off so the offline loudness work survives).</summary>
        public bool Mono = true;
        /// <summary>Load type.</summary>
        public AudioClipLoadType LoadType = AudioClipLoadType.DecompressOnLoad;
        /// <summary>Preload audio data with the scene/bundle.</summary>
        public bool Preload = true;
        /// <summary>Load in background (non-preloaded VO, music, ambience).</summary>
        public bool LoadInBackground;
        /// <summary>Compression quality 0–100 (Vorbis default platform, AAC on WebGL).</summary>
        public int Quality = 70;
        /// <summary>Sample-rate override in Hz, 0 = preserve.</summary>
        public int SampleRate;

        /// <inheritdoc />
        public override string ToString() =>
            Name + ": " + (Mono ? "mono" : "stereo") + " " + LoadType + (Preload ? " preload" : " no-preload") +
            (LoadInBackground ? " background" : "") + " q" + Quality + (SampleRate > 0 ? " " + SampleRate + " Hz" : " native rate");
    }
}
