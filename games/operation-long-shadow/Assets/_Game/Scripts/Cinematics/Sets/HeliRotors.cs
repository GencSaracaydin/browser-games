using UnityEngine;

namespace LongShadow.Cinematics.Sets
{
    /// <summary>
    /// Spins Magpie's rotors (WP10_FAL: SM_HeliMainRotor about local Y at the main hub, SM_HeliTailRotor about local X
    /// at the tail hub; blades below about 150 rpm, blur discs above). Children whose names contain "Blur" are shown
    /// only at speed and the blade meshes only below it. Unscaled time, so the speed ramp of I-8 (timeline speed) is the
    /// only thing that slows them.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeliRotors : MonoBehaviour
    {
        /// <summary>Main rotor rpm in flight (UH-1 class, about 324 rpm).</summary>
        public const float MainRpm = 324f;
        /// <summary>Tail rotor rpm.</summary>
        public const float TailRpm = 1650f;
        /// <summary>Above this rpm the blur discs replace the blades.</summary>
        public const float BlurRpm = 150f;

        [SerializeField] private Transform _main;
        [SerializeField] private Transform _tail;
        [SerializeField, Range(0f, 1f)] private float _throttle = 1f;

        private float _spin = 1f;
        private float _timeScale = 1f;
        private Renderer[] _mainRenderers;
        private Renderer[] _tailRenderers;
        private bool[] _mainBlur;
        private bool[] _tailBlur;
        private int _blurState = -1;

        /// <summary>Target throttle 0..1 (spins up and down over a few seconds).</summary>
        public float Throttle { get => _throttle; set => _throttle = Mathf.Clamp01(value); }
        /// <summary>Playback scale of the rotor motion (cinematic speed ramps).</summary>
        public float TimeScale { get => _timeScale; set => _timeScale = Mathf.Max(0f, value); }

        /// <summary>Wiring (InsertionCast).</summary>
        public void Setup(Transform main, Transform tail)
        {
            _main = main;
            _tail = tail;
            _mainRenderers = main != null ? main.GetComponentsInChildren<Renderer>(true) : null;
            _tailRenderers = tail != null ? tail.GetComponentsInChildren<Renderer>(true) : null;
            _mainBlur = BlurFlags(_mainRenderers);
            _tailBlur = BlurFlags(_tailRenderers);
            _blurState = -1;
        }

        private void Awake()
        {
            if (_main == null) _main = FindChild(transform, "MainRotor");
            if (_tail == null) _tail = FindChild(transform, "TailRotor");
            Setup(_main, _tail);
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime * _timeScale;
            _spin = Mathf.MoveTowards(_spin, _throttle, Time.unscaledDeltaTime / 6f);
            var rpm = MainRpm * _spin;
            if (_main != null) _main.Rotate(0f, rpm * 6f * dt, 0f, Space.Self);
            if (_tail != null) _tail.Rotate(TailRpm * _spin * 6f * dt, 0f, 0f, Space.Self);
            var blur = rpm * Mathf.Max(0.05f, _timeScale) > BlurRpm;
            var state = blur ? 1 : 0;
            if (state == _blurState) return;
            _blurState = state;
            Swap(_mainRenderers, _mainBlur, blur);
            Swap(_tailRenderers, _tailBlur, blur);
        }

        private static bool[] BlurFlags(Renderer[] renderers)
        {
            if (renderers == null) return null;
            var flags = new bool[renderers.Length];
            var any = false;
            for (var i = 0; i < renderers.Length; i++)
            {
                flags[i] = renderers[i] != null && renderers[i].name.IndexOf("Blur", System.StringComparison.OrdinalIgnoreCase) >= 0;
                any |= flags[i];
            }
            return any ? flags : null;
        }

        private static void Swap(Renderer[] renderers, bool[] isBlur, bool blur)
        {
            if (renderers == null || isBlur == null) return;
            for (var i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].enabled = isBlur[i] == blur;
        }

        /// <summary>First descendant whose name contains <paramref name="part"/> (case-insensitive).</summary>
        public static Transform FindChild(Transform root, string part)
        {
            if (root == null) return null;
            foreach (Transform c in root)
            {
                if (c.name.IndexOf(part, System.StringComparison.OrdinalIgnoreCase) >= 0) return c;
                var deep = FindChild(c, part);
                if (deep != null) return deep;
            }
            return null;
        }
    }
}
