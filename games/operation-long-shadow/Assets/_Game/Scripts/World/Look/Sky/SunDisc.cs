using LongShadow.Core;
using LongShadow.World.Look.Common;
using UnityEngine;
using UnityEngine.Rendering;

namespace LongShadow.World.Look.Sky
{
    /// <summary>
    /// The visible sun (GDD §9.3, TECH_PLAN §7.14): an HDR emissive billboard 0.53° wide, placed 2.9 km from each camera
    /// along the sun direction just before that camera renders, so it is unaffected by distance and blooms. Its shader
    /// (<c>LongShadow/World/SunDisc</c>) ignores fog and depth-tests normally, so the western ridge silhouette at 2.5 km
    /// hides it in MID-A. Driven by <see cref="Stage.LightStageController"/> through <see cref="SetState"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SunDisc : MonoBehaviour
    {
        /// <summary>The billboard quad is this much wider than the disc (room for the limb glow).</summary>
        public const float QuadOverscan = 1.6f;

        [SerializeField, Tooltip("Quad renderer using M_SunDisc.")]
        private MeshRenderer _renderer;
        [SerializeField, Tooltip("Cameras on these layers are skipped (render-texture feeds may still want the disc; booth cameras never see layer Default).")]
        private LayerMask _ignoreCameraLayers;

        private MaterialPropertyBlock _mpb;
        private Vector3 _toSun = Vector3.up;
        private Color _color;
        private float _diameterDeg = 0.53f;
        private float _distance = 2900f;
        private bool _visible;

        /// <summary>Direction toward the sun.</summary>
        public Vector3 ToSun => _toSun;
        /// <summary>True while the disc renders.</summary>
        public bool Visible => _visible;

        private void Awake()
        {
            if (_renderer == null) _renderer = GetComponentInChildren<MeshRenderer>();
            if (_renderer != null)
            {
                _renderer.shadowCastingMode = ShadowCastingMode.Off;
                _renderer.receiveShadows = false;
                _renderer.lightProbeUsage = LightProbeUsage.Off;
                _renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                _renderer.allowOcclusionWhenDynamic = false;
            }
            _mpb = new MaterialPropertyBlock();
        }

        private void OnEnable() => RenderPipelineManager.beginCameraRendering += OnBeginCamera;

        private void OnDisable() => RenderPipelineManager.beginCameraRendering -= OnBeginCamera;

        /// <summary>
        /// Sets the disc. <paramref name="toSun"/> points from the scene toward the sun; <paramref name="hdrColor"/> is the
        /// linear HDR colour of the disc core (already multiplied by intensity and visibility; black hides it).
        /// </summary>
        public void SetState(Vector3 toSun, Color hdrColor, float diameterDegrees, float distance)
        {
            _toSun = toSun.sqrMagnitude > 1e-8f ? toSun.normalized : Vector3.up;
            _color = hdrColor;
            _diameterDeg = Mathf.Max(0.01f, diameterDegrees);
            _distance = Mathf.Max(10f, distance);
            _visible = hdrColor.maxColorComponent > 1e-4f;
            if (_renderer == null) return;
            _renderer.enabled = _visible;
            if (!_visible) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(LookShaderIds.BaseColor, hdrColor);
            _renderer.SetPropertyBlock(_mpb);
        }

        /// <summary>Places the billboard for a camera (also used by LookCapture in the editor).</summary>
        public void PlaceFor(Camera cam)
        {
            if (cam == null || _renderer == null) return;
            var t = _renderer.transform;
            var p = cam.transform.position;
            var far = cam.farClipPlane > 20f ? Mathf.Min(_distance, cam.farClipPlane * 0.97f) : _distance;
            t.position = p + _toSun * far;
            t.rotation = Quaternion.LookRotation(-_toSun, Mathf.Abs(_toSun.y) > 0.99f ? Vector3.forward : Vector3.up);
            var size = 2f * far * Mathf.Tan(0.5f * _diameterDeg * Mathf.Deg2Rad) * QuadOverscan;
            t.localScale = new Vector3(size, size, size);
        }

        private void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
        {
            if (!_visible || cam == null) return;
            if ((_ignoreCameraLayers.value & (1 << cam.gameObject.layer)) != 0) return;
            PlaceFor(cam);
        }

#if UNITY_EDITOR
        /// <summary>Editor builders: wires the quad.</summary>
        public void EditorSetup(MeshRenderer quad) => _renderer = quad;
#endif
    }
}
