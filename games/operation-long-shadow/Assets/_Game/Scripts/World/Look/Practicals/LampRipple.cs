using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.World.Look.Common;
using LongShadow.World.Look.Stage;
using UnityEngine;

namespace LongShadow.World.Look.Practicals
{
    /// <summary>
    /// The MID-A sodium ripple (GDD §8.5 row 4, TECH_PLAN §7.14 <c>LampRipple</c>): on <see cref="GameEvents.PracticalsStrike"/>
    /// every <see cref="SodiumLamp"/> strikes after (horizontal distance from the station generator at (372, 0, 305)) /
    /// 15 m/s, "street by street". The few lamps nearest the listener also play <c>SFX_SODIUM_STRIKE</c> at their
    /// position (ballast tick and buzz). Going back below s 1.97 (checkpoint) resets the ripple. One instance lives on
    /// <c>PF_LookRig</c>; the static state lets lamps that load mid-ripple join it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LampRipple : MonoBehaviour
    {
        /// <summary>Station generator, ripple origin (GDD §8.5).</summary>
        public static readonly Vector3 DefaultOrigin = new Vector3(372f, 0f, 305f);
        /// <summary>Ripple speed m/s (GDD §8.5).</summary>
        public const float DefaultSpeed = 15f;

        private struct PendingSound
        {
            public float Time;
            public Vector3 Position;
        }

        [SerializeField] private Vector3 _origin = new Vector3(372f, 0f, 305f);
        [SerializeField] private float _speed = DefaultSpeed;
        [SerializeField, Tooltip("Strike sounds per ripple (nearest lamps to the listener).")]
        private int _maxSounds = 6;
        [SerializeField, Tooltip("Only lamps this close to the listener play a strike sound.")]
        private float _soundRadius = 45f;
        [SerializeField] private string _strikeCue = LookIds.CueSodiumStrike;

        private static bool s_struck;
        private static float s_strikeTime;
        private static Vector3 s_origin = new Vector3(372f, 0f, 305f);
        private static float s_speed = DefaultSpeed;
        private static float s_rippleEnd;

        private readonly List<PendingSound> _sounds = new List<PendingSound>(8);
        private readonly List<float> _soundDistances = new List<float>(64);
        private ServiceRef<IAudioService> _audio;
        private ServiceRef<IPlayerRig> _rig;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_struck = false;
            s_strikeTime = 0f;
            s_origin = DefaultOrigin;
            s_speed = DefaultSpeed;
            s_rippleEnd = 0f;
        }

        /// <summary>True after a strike (until s goes back below 1.97).</summary>
        public static bool HasStruck => s_struck;
        /// <summary>True while the ripple is still travelling or warming.</summary>
        public static bool IsRippling => s_struck && Time.time < s_rippleEnd;

        /// <summary>Strike time of a lamp at <paramref name="position"/> for the last ripple.</summary>
        public static float StrikeTimeFor(Vector3 position) => s_strikeTime + Delay(position, s_origin, s_speed);

        /// <summary>Ripple delay of a lamp (horizontal distance / speed).</summary>
        public static float Delay(Vector3 position, Vector3 origin, float speed)
        {
            var dx = position.x - origin.x;
            var dz = position.z - origin.z;
            return Mathf.Sqrt(dx * dx + dz * dz) / Mathf.Max(0.1f, speed);
        }

        private void OnEnable()
        {
            s_origin = _origin;
            s_speed = _speed;
            GameEvents.PracticalsStrike += OnStrike;
            GameEvents.LightStageChanged += OnStageChanged;
        }

        private void OnDisable()
        {
            GameEvents.PracticalsStrike -= OnStrike;
            GameEvents.LightStageChanged -= OnStageChanged;
            _sounds.Clear();
        }

        /// <summary>Starts the ripple now (also used by the editor scrubber and tests).</summary>
        public void Strike()
        {
            s_struck = true;
            s_strikeTime = Time.time;
            var maxDelay = 0f;
            var lamps = SodiumLamp.All;
            for (var i = 0; i < lamps.Count; i++)
            {
                var lamp = lamps[i];
                if (lamp == null) continue;
                var d = Delay(lamp.transform.position, _origin, _speed);
                if (d > maxDelay) maxDelay = d;
                lamp.ScheduleStrike(s_strikeTime + d);
            }
            // Far lamps in sections that are not loaded can still join: cover the whole town (≈ 400 m).
            s_rippleEnd = s_strikeTime + Mathf.Max(maxDelay, 400f / Mathf.Max(0.1f, _speed)) + SodiumWarmup.Duration;
            QueueSounds(lamps);
        }

        private void OnStrike() => Strike();

        private void OnStageChanged(float s)
        {
            if (s < LightStageTable.StrikeS - 0.005f)
            {
                s_struck = false;
                s_rippleEnd = 0f;
                _sounds.Clear();
            }
        }

        private void QueueSounds(IReadOnlyList<SodiumLamp> lamps)
        {
            _sounds.Clear();
            if (_audio.Value == null || _maxSounds <= 0) return;
            var listener = ListenerPosition();
            // Pick the nearest lamps to the listener (N is small; a selection pass per slot, no allocation).
            _soundDistances.Clear();
            for (var i = 0; i < lamps.Count; i++)
                _soundDistances.Add(lamps[i] != null ? (lamps[i].transform.position - listener).sqrMagnitude : float.MaxValue);
            var r2 = _soundRadius * _soundRadius;
            for (var n = 0; n < _maxSounds; n++)
            {
                var best = -1;
                var bestD = r2;
                for (var i = 0; i < _soundDistances.Count; i++)
                {
                    if (_soundDistances[i] >= bestD) continue;
                    bestD = _soundDistances[i];
                    best = i;
                }
                if (best < 0) break;
                _soundDistances[best] = float.MaxValue;
                var p = lamps[best].transform.position;
                _sounds.Add(new PendingSound { Time = s_strikeTime + Delay(p, _origin, _speed), Position = p });
            }
        }

        private void Update()
        {
            if (_sounds.Count == 0) return;
            var audio = _audio.Value;
            for (var i = _sounds.Count - 1; i >= 0; i--)
            {
                if (Time.time < _sounds[i].Time) continue;
                if (audio != null && !string.IsNullOrEmpty(_strikeCue)) audio.Play(_strikeCue, _sounds[i].Position, 0.8f);
                _sounds.RemoveAt(i);
            }
        }

        private Vector3 ListenerPosition()
        {
            var rig = _rig.Value;
            if (rig != null && rig.Eye != null) return rig.Eye.position;
            var cam = Camera.main;
            return cam != null ? cam.transform.position : transform.position;
        }
    }
}
