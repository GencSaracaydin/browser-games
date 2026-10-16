using LongShadow.Core;
using LongShadow.World.Look.Common;
using LongShadow.World.Look.Stage;
using UnityEngine;
using UnityEngine.Rendering;

namespace LongShadow.World.Look.Atmosphere
{
    /// <summary>
    /// A sun shaft (GDD §9.7: grove, market cross-alleys, torn awnings, cinema shrapnel holes; PROC_LIGHT_SHAFTS): an
    /// additive card (<c>LongShadow/World/LightShaft</c>) from an aperture along the light's travel direction, turned
    /// around that axis to face each camera. It follows the sun, takes the sun's colour and intensity, fades with the
    /// stage, and checks every half second that its aperture really sees the sun (a raycast toward the sun), so no shaft
    /// glows where the shadow map says shade (WP10 DoD). Optional dust motes ride inside it. With
    /// <see cref="_followSun"/> off (projector beam, flood cones) it keeps its own direction and colour.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LightShaft : MonoBehaviour
    {
        [SerializeField, Tooltip("Card renderer (quad 1 x 1, pivot at the aperture end, long axis = local +Y).")]
        private MeshRenderer _card;
        [SerializeField, Tooltip("Optional motes inside the beam.")]
        private ParticleSystem _motes;
        [SerializeField, Tooltip("Shaft length in metres.")]
        private float _length = 8f;
        [SerializeField, Tooltip("Shaft width in metres.")]
        private float _width = 1.2f;
        [SerializeField, Tooltip("Aim along the sun's light (otherwise along this transform's forward).")]
        private bool _followSun = true;
        [SerializeField, Tooltip("Card brightness per unit sun intensity.")]
        private float _gain = 0.08f;
        [SerializeField, ColorUsage(false, true), Tooltip("Colour when not following the sun (projector beam).")]
        private Color _fixedColor = new Color(1f, 0.95f, 0.85f);
        [SerializeField, Tooltip("Layers that can shade the aperture.")]
        private LayerMask _occluders = 1 << LayerIds.Default;
        [SerializeField, Tooltip("Seconds between sun visibility checks.")]
        private float _checkInterval = 0.5f;
        [SerializeField, Tooltip("Fade seconds when the aperture enters or leaves shade.")]
        private float _fadeSeconds = 0.8f;
        [SerializeField, Tooltip("Motes emission at full brightness (per second).")]
        private float _motesRate = 18f;

        private MaterialPropertyBlock _mpb;
        private float _lit = 1f;
        private float _litTarget = 1f;
        private float _nextCheck;
        private Vector3 _axis = Vector3.down;
        private float _intensity;
        private Color _color;

        /// <summary>Current brightness 0.. (after occlusion and stage).</summary>
        public float Intensity => _intensity;
        /// <summary>True when the last check found the aperture in the sun.</summary>
        public bool ApertureLit => _litTarget > 0.5f;

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            if (_card != null)
            {
                _card.shadowCastingMode = ShadowCastingMode.Off;
                _card.receiveShadows = false;
                _card.lightProbeUsage = LightProbeUsage.Off;
                _card.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
        }

        private void OnEnable()
        {
            _nextCheck = 0f;
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        }

        private void OnDisable() => RenderPipelineManager.beginCameraRendering -= OnBeginCamera;

        private void Update()
        {
            var ls = LightStageController.Active;
            Color c;
            float strength;
            if (_followSun)
            {
                if (ls == null || !ls.CurrentSample.SunEnabled)
                {
                    c = Color.black;
                    strength = 0f;
                }
                else
                {
                    var s = ls.CurrentSample;
                    _axis = -LightStageTable.SunDirection(s.SunElevation);
                    c = ls.SunColorLinear;
                    strength = s.SunDiscVisibility;
                }
            }
            else
            {
                _axis = transform.forward;
                c = _fixedColor;
                strength = 1f;
            }

            if (Time.time >= _nextCheck)
            {
                _nextCheck = Time.time + Mathf.Max(0.05f, _checkInterval);
                _litTarget = !_followSun || ApertureSeesSun() ? 1f : 0f;
            }
            var rate = _fadeSeconds > 0f ? Time.deltaTime / _fadeSeconds : 1f;
            _lit = Mathf.MoveTowards(_lit, _litTarget, rate);

            _intensity = strength * _lit;
            _color = c * (_gain * _intensity);
            if (_card != null)
            {
                var on = _intensity > 1e-3f;
                if (_card.enabled != on) _card.enabled = on;
                if (on)
                {
                    _card.GetPropertyBlock(_mpb);
                    _mpb.SetColor(LookShaderIds.BaseColor, _color);
                    _card.SetPropertyBlock(_mpb);
                }
            }
            if (_motes != null)
            {
                var em = _motes.emission;
                em.rateOverTimeMultiplier = _motesRate * _intensity;
                var main = _motes.main;
                main.startColor = new Color(Mathf.Min(1f, c.r), Mathf.Min(1f, c.g), Mathf.Min(1f, c.b), 1f);
            }
        }

        private bool ApertureSeesSun()
        {
            var origin = transform.position - _axis * 0.05f;
            return !Physics.Raycast(origin, -_axis, 400f, _occluders, QueryTriggerInteraction.Ignore);
        }

        private void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
        {
            if (_card == null || !_card.enabled || cam == null) return;
            var t = _card.transform;
            var axis = _axis.sqrMagnitude > 1e-6f ? _axis.normalized : Vector3.down;
            var toCam = cam.transform.position - transform.position;
            var side = Vector3.Cross(axis, toCam);
            if (side.sqrMagnitude < 1e-6f) side = Vector3.Cross(axis, Vector3.up);
            if (side.sqrMagnitude < 1e-6f) side = Vector3.right;
            side.Normalize();
            var normal = Vector3.Cross(side, axis);
            // Quad: local X = width (side), local Y = length (axis), facing the camera (-Z toward the camera).
            t.SetPositionAndRotation(transform.position + axis * (0.5f * _length), Quaternion.LookRotation(-normal, axis));
            t.localScale = new Vector3(_width, _length, 1f);
        }

#if UNITY_EDITOR
        /// <summary>Editor builders.</summary>
        public void EditorSetup(MeshRenderer card, ParticleSystem motes, float length, float width, bool followSun)
        {
            _card = card;
            _motes = motes;
            _length = length;
            _width = width;
            _followSun = followSun;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.5f, 0.6f);
            var axis = _followSun ? -LightStageTable.SunDirection(5f) : transform.forward;
            Gizmos.DrawLine(transform.position, transform.position + axis * _length);
        }
#endif
    }
}
