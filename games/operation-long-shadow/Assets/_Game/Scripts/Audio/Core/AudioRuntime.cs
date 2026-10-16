using System;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Audio.Core
{
    /// <summary>
    /// Shared state of the audio package (inside the LongShadow.Audio assembly only): the one <see cref="AudioMix"/>,
    /// the listener pose and acoustic zone published by the <see cref="AudioService"/>, the insert/combat flags, and the
    /// VO-window notification the MusicDirector listens to. Everything works without an AudioService (tests, partial
    /// scenes): the mix is neutral and the listener falls back to the main camera. Statics reset on
    /// SubsystemRegistration (R6).
    /// </summary>
    internal static class AudioRuntime
    {
        private static AudioMix s_mix = new AudioMix();
        private static int s_nextKey = 1;
        private static bool s_hasListener;
        private static Vector3 s_listenerPosition;
        private static Quaternion s_listenerRotation = Quaternion.identity;
        private static Func<float> s_clock;

        /// <summary>The mix every audio owner applies.</summary>
        public static AudioMix Mix => s_mix;

        /// <summary>Acoustic zone of the listener (AudioService, ZoneTag).</summary>
        public static ZoneType Zone { get; set; } = ZoneType.OpenDesert;

        /// <summary>True between InsertStarted and InsertEnded (gameplay audio is paused; insert audio ignores the pause).</summary>
        public static bool InsertActive { get; set; }

        /// <summary>True while any enemy is in combat (GameEvents.CombatStateChanged).</summary>
        public static bool InCombat { get; set; }

        /// <summary>Raised by the VOManager when a beat window is prepared (from, to).</summary>
        public static event Action<int, int> WindowPrepared;

        /// <summary>
        /// Raised by the AudioService when the listener pauses or resumes (the pause menu, or a glitch insert freezing the
        /// world): owners of scheduled loops remember their position and restart there (<see cref="ScheduledLooper"/>).
        /// </summary>
        public static event Action<bool> ListenerPaused;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_mix = new AudioMix();
            s_nextKey = 1;
            s_hasListener = false;
            s_listenerPosition = Vector3.zero;
            s_listenerRotation = Quaternion.identity;
            s_clock = null;
            Zone = ZoneType.OpenDesert;
            InsertActive = false;
            InCombat = false;
            WindowPrepared = null;
            ListenerPaused = null;
        }

        /// <summary>A unique key for <see cref="AudioMix.SetHold"/> owners.</summary>
        public static int NewKey() => s_nextKey++;

        /// <summary>Unscaled seconds (overridable by tests).</summary>
        public static float Now => s_clock != null ? s_clock() : Time.unscaledTime;

        /// <summary>Tests: replaces the clock (null restores <c>Time.unscaledTime</c>).</summary>
        public static void SetClockForTests(Func<float> clock) => s_clock = clock;

        /// <summary>Tests: a fresh mix and default state.</summary>
        public static void ResetForTests() => ResetStatics();

        /// <summary>Published by the AudioService every LateUpdate.</summary>
        public static void SetListener(Vector3 position, Quaternion rotation)
        {
            s_hasListener = true;
            s_listenerPosition = position;
            s_listenerRotation = rotation;
        }

        /// <summary>Listener position (AudioService), else the main camera, else the origin.</summary>
        public static Vector3 ListenerPosition
        {
            get
            {
                if (s_hasListener) return s_listenerPosition;
                var cam = Camera.main;
                return cam != null ? cam.transform.position : Vector3.zero;
            }
        }

        /// <summary>Listener rotation (AudioService), else the main camera, else identity.</summary>
        public static Quaternion ListenerRotation
        {
            get
            {
                if (s_hasListener) return s_listenerRotation;
                var cam = Camera.main;
                return cam != null ? cam.transform.rotation : Quaternion.identity;
            }
        }

        /// <summary>Gain of a category for a source started inside or outside a glitch insert.</summary>
        public static float Gain(MixCategory c, bool duringInsert) => duringInsert ? s_mix.GainDuringInsert(c) : s_mix.Gain(c);

        /// <summary>Notifies scheduled-loop owners that the listener paused (true) or resumed (false).</summary>
        public static void RaiseListenerPaused(bool paused) => ListenerPaused?.Invoke(paused);

        /// <summary>Notifies listeners that a VO beat window was prepared.</summary>
        public static void RaiseWindowPrepared(int from, int to) => WindowPrepared?.Invoke(from, to);
    }
}
