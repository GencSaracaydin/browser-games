using UnityEngine;

namespace LongShadow.Cinematics.Booth
{
    /// <summary>
    /// The stopped wall clock (GDD §5.1: "stopped at 6:40", §8.2 shot 2: "second hand twitching in place"). PH_WALL_CLOCK
    /// conventions (WP09_ART): hands pivot on the dial centre and are exported at 12:00; hour local Z = −(h + m/60)·30°,
    /// minute = −m·6°, second = −s·6° (negative = clockwise seen from the front). The second hand tries to advance and
    /// falls back, with the stuck tick on every attempt (SFX_CLOCK_TICK_STUCK).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WallClock : MonoBehaviour
    {
        /// <summary>Hours shown (6:40).</summary>
        public const int Hours = 6;
        /// <summary>Minutes shown.</summary>
        public const int Minutes = 40;
        /// <summary>Second the hand is stuck at.</summary>
        public const float StuckSecond = 23f;

        [SerializeField] private Transform _hour;
        [SerializeField] private Transform _minute;
        [SerializeField] private Transform _second;
        [SerializeField] private bool _twitch = true;
        [SerializeField] private bool _tickSound;

        private Quaternion _hourRest = Quaternion.identity;
        private Quaternion _minuteRest = Quaternion.identity;
        private Quaternion _secondRest = Quaternion.identity;
        private float _clock;

        private void Awake()
        {
            if (_hour != null) _hourRest = _hour.localRotation;
            if (_minute != null) _minuteRest = _minute.localRotation;
            if (_second != null) _secondRest = _second.localRotation;
            SetHands();
        }

        /// <summary>Builder wiring.</summary>
        public void Setup(Transform hour, Transform minute, Transform second)
        {
            _hour = hour;
            _minute = minute;
            _second = second;
        }

        /// <summary>Twitching second hand on/off; ticking audible on/off (intro shot 1 and 2).</summary>
        public void SetTwitch(bool twitch, bool tickSound)
        {
            _twitch = twitch;
            _tickSound = tickSound;
        }

        /// <summary>Hour-hand angle (degrees about local Z) for a time.</summary>
        public static float HourAngle(int h, int m) => -((h % 12) + m / 60f) * 30f;

        /// <summary>Minute-hand angle (degrees about local Z) for a time.</summary>
        public static float MinuteAngle(int m) => -m * 6f;

        private void SetHands()
        {
            if (_hour != null) _hour.localRotation = _hourRest * Quaternion.Euler(0f, 0f, HourAngle(Hours, Minutes));
            if (_minute != null) _minute.localRotation = _minuteRest * Quaternion.Euler(0f, 0f, MinuteAngle(Minutes));
        }

        private void Update()
        {
            if (_second == null) return;
            var prev = _clock;
            _clock += Time.unscaledDeltaTime;
            // Once a second the hand jumps forward 1.2° and creeps back over 0.25 s: it never advances.
            var phase = _clock % 1f;
            var kick = !_twitch ? 0f : phase < 0.05f ? Mathf.Lerp(0f, 1.2f, phase / 0.05f) : Mathf.Lerp(1.2f, 0f, Mathf.Clamp01((phase - 0.05f) / 0.25f));
            _second.localRotation = _secondRest * Quaternion.Euler(0f, 0f, -StuckSecond * 6f - kick);
            if (_tickSound && _twitch && Mathf.Floor(prev) != Mathf.Floor(_clock))
                CinematicAudio.Play(CinematicCues.ClockTickStuck, transform.position, 0.8f);
        }
    }
}
