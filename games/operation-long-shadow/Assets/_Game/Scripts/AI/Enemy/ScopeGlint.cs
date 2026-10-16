using LongShadow.Core;
using UnityEngine;

namespace LongShadow.AI.Enemy
{
    /// <summary>
    /// The marksman's scope glint (GDD §7.5: "a physically motivated flare (the sun is behind the player) that shows
    /// 1.5 s before each shot"): a camera-facing HDR quad at the objective lens that flares and twinkles while the scope
    /// points at the player. Its apparent size is held at about 0.35° so it reads at 120 m and never looks like a lamp
    /// up close; the HDR colour drives bloom. The renderer and material are made by EnemyPrefabBuilder
    /// (M_AI_ScopeGlint, URP Unlit); without them a runtime quad is created.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScopeGlint : MonoBehaviour
    {
        /// <summary>Apparent angular size in degrees.</summary>
        public const float ApparentDegrees = 0.35f;
        /// <summary>Smallest world size (m).</summary>
        public const float MinSize = 0.08f;

        [SerializeField] private Renderer _renderer;
        [SerializeField, Tooltip("HDR colour at full flare.")] private Color _color = new Color(6f, 5.6f, 4.8f, 1f);

        private ServiceRef<IPlayerRig> _rig;
        private MaterialPropertyBlock _block;
        private float _shownAt = -1f;
        private float _intensity;
        private static readonly int s_baseColor = Shader.PropertyToID("_BaseColor");

        /// <summary>True while the glint shows.</summary>
        public bool Visible => _shownAt >= 0f;

        /// <summary>Binds a renderer (builders).</summary>
        public void Configure(Renderer renderer) => _renderer = renderer;

        private void Awake()
        {
            EnsureRenderer();
            Hide();
        }

        private void EnsureRenderer()
        {
            if (_renderer != null) return;
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "PH_GlintQuad";
            var c = quad.GetComponent<Collider>();
            if (c != null) Destroy(c);
            quad.transform.SetParent(transform, false);
            _renderer = quad.GetComponent<Renderer>();
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            _renderer.sharedMaterial = shader != null ? new Material(shader) { name = "M_AI_ScopeGlint_Runtime" } : Placeholders.Material(Color.white);
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
        }

        /// <summary>Starts the flare.</summary>
        public void Show()
        {
            if (_shownAt >= 0f) return;
            _shownAt = Time.time;
            if (_renderer != null) _renderer.enabled = true;
        }

        /// <summary>Stops the flare.</summary>
        public void Hide()
        {
            _shownAt = -1f;
            _intensity = 0f;
            if (_renderer != null) _renderer.enabled = false;
        }

        private void LateUpdate()
        {
            if (_shownAt < 0f || _renderer == null) return;
            var cam = _rig.Value != null ? _rig.Value.WorldCamera : null;
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            var camPos = cam.transform.position;
            var to = camPos - transform.position;
            var d = to.magnitude;
            if (d < 1e-3f) return;
            var t = _renderer.transform;
            t.rotation = Quaternion.LookRotation(-to / d, cam.transform.up);
            // Rise over 0.25 s, then a slow twinkle as the scope drifts (hand tremor at 120 m).
            var age = Time.time - _shownAt;
            var rise = Mathf.Clamp01(age / 0.25f);
            var twinkle = 0.75f + 0.25f * Mathf.Sin(age * 17f) * Mathf.Sin(age * 5.3f + 1.1f);
            _intensity = rise * twinkle;
            var size = Mathf.Max(MinSize, d * Mathf.Tan(ApparentDegrees * Mathf.Deg2Rad));
            t.localScale = new Vector3(size, size, size) * Mathf.Lerp(0.6f, 1f, _intensity);
            if (_block == null) _block = new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_block);
            _block.SetColor(s_baseColor, _color * _intensity);
            _renderer.SetPropertyBlock(_block);
        }
    }
}
