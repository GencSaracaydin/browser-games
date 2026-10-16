using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Cinematics
{
    /// <summary>
    /// Null-safe access to WP06's audio services for the booth, inserts and timeline clips. Every call degrades to a
    /// no-op when the service (or the cue) is missing, so cinematics keep their timing in test scenes.
    /// </summary>
    public static class CinematicAudio
    {
        private static ServiceRef<IAudioService> s_audio;
        private static ServiceRef<IMusicService> s_music;
        private static ServiceRef<IVOService> s_vo;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_audio = default;
            s_music = default;
            s_vo = default;
        }

        /// <summary>The audio service or null.</summary>
        public static IAudioService Audio => s_audio.Value;
        /// <summary>The music service or null.</summary>
        public static IMusicService Music => s_music.Value;
        /// <summary>The VO service or null.</summary>
        public static IVOService VO => s_vo.Value;

        /// <summary>3D one-shot at a position.</summary>
        public static void Play(string cueId, Vector3 position, float volume = 1f)
        {
            if (string.IsNullOrEmpty(cueId)) return;
            s_audio.Value?.Play(cueId, position, volume);
        }

        /// <summary>2D one-shot.</summary>
        public static void Play2D(string cueId, float volume = 1f)
        {
            if (string.IsNullOrEmpty(cueId)) return;
            s_audio.Value?.Play2D(cueId, volume);
        }

        /// <summary>Loop following a transform; returns a handle (0 when nothing plays).</summary>
        public static int Loop(string cueId, Transform follow)
        {
            if (string.IsNullOrEmpty(cueId)) return 0;
            var a = s_audio.Value;
            return a != null ? a.PlayLoop(cueId, follow) : 0;
        }

        /// <summary>One-shot attached to a moving transform; returns a handle (0 when nothing plays).</summary>
        public static int Attached(string cueId, Transform follow, float volume = 1f)
        {
            if (string.IsNullOrEmpty(cueId)) return 0;
            var a = s_audio.Value;
            return a != null ? a.PlayAttached(cueId, follow, volume) : 0;
        }

        /// <summary>Stops a loop or attached handle with a fade.</summary>
        public static void Stop(int handle, float fade)
        {
            if (handle == 0) return;
            s_audio.Value?.StopLoop(handle, fade);
        }

        /// <summary>Music stinger, falling back to a 2D one-shot when there is no music service.</summary>
        public static void Stinger(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            var m = s_music.Value;
            if (m != null) m.Stinger(id);
            else s_audio.Value?.Play2D(id);
        }

        /// <summary>Envelope amplitude 0..1 of what a speaker is saying now (VU needle, jaw flap; TECH_PLAN D10).</summary>
        public static float Amplitude(string speakerId)
        {
            var v = s_vo.Value;
            return v != null ? Mathf.Clamp01(v.Amplitude(speakerId)) : 0f;
        }
    }
}
