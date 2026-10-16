using UnityEngine;

namespace LongShadow.Cinematics.Booth
{
    /// <summary>
    /// The reel-to-reel deck playing TALLOW's briefing tape (GDD §8.2 shot 5f, live on CRT3 through the deck feed).
    /// FAL_REEL_TO_REEL conventions (WP09_ART): both reels turn counter-clockwise seen from above in playback (negative
    /// local Y rate), deg/s = ips·0.0254 / (2π·r) · 360 at 7.5 ips with r = 0.075 m × pack scale; the supply pack shrinks
    /// (1.0 → 0.42) while the take-up pack grows.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ReelDeck : MonoBehaviour
    {
        /// <summary>Tape speed in inches per second.</summary>
        public const float Ips = 7.5f;
        /// <summary>Full-pack radius in metres.</summary>
        public const float PackRadius = 0.075f;
        /// <summary>Empty pack scale (hub).</summary>
        public const float EmptyPack = 0.42f;

        [SerializeField] private Transform _supplyReel;
        [SerializeField] private Transform _takeupReel;
        [SerializeField] private Transform _supplyPack;
        [SerializeField] private Transform _takeupPack;
        [SerializeField, Tooltip("Seconds of tape on a full reel at 7.5 ips (a 1800 ft 7-inch reel is ~48 min).")]
        private float _tapeSeconds = 2880f;
        [SerializeField, Range(0f, 1f)] private float _position = 0.35f;

        private bool _playing;
        private float _speed;

        /// <summary>True while playing.</summary>
        public bool Playing => _playing;

        /// <summary>Builder wiring.</summary>
        public void Setup(Transform supplyReel, Transform takeupReel, Transform supplyPack, Transform takeupPack)
        {
            _supplyReel = supplyReel;
            _takeupReel = takeupReel;
            _supplyPack = supplyPack;
            _takeupPack = takeupPack;
        }

        /// <summary>Starts or stops playback (spins up and down over a fraction of a second).</summary>
        public void SetPlaying(bool playing) => _playing = playing;

        /// <summary>Angular speed in degrees per second of a reel with a pack scale (pure; tests).</summary>
        public static float ReelDegreesPerSecond(float packScale)
        {
            var r = Mathf.Max(0.01f, PackRadius * packScale);
            return Ips * 0.0254f / (2f * Mathf.PI * r) * 360f;
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            _speed = Mathf.MoveTowards(_speed, _playing ? 1f : 0f, dt / 0.4f);
            if (_speed <= 0f) return;
            _position = Mathf.Clamp01(_position + _speed * dt / Mathf.Max(1f, _tapeSeconds));
            var supply = PackScale(1f - _position);
            var takeup = PackScale(_position);
            if (_supplyPack != null) _supplyPack.localScale = new Vector3(supply, 1f, supply);
            if (_takeupPack != null) _takeupPack.localScale = new Vector3(takeup, 1f, takeup);
            if (_supplyReel != null) _supplyReel.Rotate(0f, -ReelDegreesPerSecond(supply) * _speed * dt, 0f, Space.Self);
            if (_takeupReel != null) _takeupReel.Rotate(0f, -ReelDegreesPerSecond(takeup) * _speed * dt, 0f, Space.Self);
        }

        /// <summary>Pack scale for a fraction of the tape on that reel (area-correct).</summary>
        public static float PackScale(float fraction)
        {
            var hub2 = EmptyPack * EmptyPack;
            return Mathf.Sqrt(hub2 + Mathf.Clamp01(fraction) * (1f - hub2));
        }
    }
}
