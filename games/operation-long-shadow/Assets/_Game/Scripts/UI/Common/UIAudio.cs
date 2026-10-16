using LongShadow.Core;
using UnityEngine;

namespace LongShadow.UI.Common
{
    /// <summary>
    /// UI sound playback through <see cref="IAudioService"/> (2D, UI category) and stingers through
    /// <see cref="IMusicService"/>. Silent when the services are missing (test scenes). Allocation-free.
    /// </summary>
    public sealed class UIAudio
    {
        private ServiceRef<IAudioService> _audio;
        private ServiceRef<IMusicService> _music;
        private float _lastTypeKey = -1f;

        /// <summary>Plays a 2D cue.</summary>
        public void Play(string cueId, float volume = 1f)
        {
            if (string.IsNullOrEmpty(cueId)) return;
            _audio.Value?.Play2D(cueId, volume);
        }

        /// <summary>Plays the typewriter key, at most once every <paramref name="minInterval"/> seconds (unscaled).</summary>
        public void TypeKey(float volume = 0.5f, float minInterval = 0.05f)
        {
            var now = Time.unscaledTime;
            if (now - _lastTypeKey < minInterval && _lastTypeKey >= 0f) return;
            _lastTypeKey = now;
            Play(UiCues.TypeKey, volume);
        }

        /// <summary>Plays a music stinger.</summary>
        public void Stinger(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            _music.Value?.Stinger(id);
        }
    }
}
