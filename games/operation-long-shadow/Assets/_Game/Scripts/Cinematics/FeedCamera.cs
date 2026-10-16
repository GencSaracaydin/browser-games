using LongShadow.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LongShadow.Cinematics
{
    /// <summary>
    /// A camera rendering into a 512 × 384 render texture for a CRT or the MID-B screen (TECH_PLAN §3.6 Cam_Feed_*):
    /// renderer 1 (PC_Renderer_RT), no post, enabled only while someone holds it (<see cref="Acquire"/>). An optional
    /// frame-rate cap steps the image (the 16 mm film feed runs at 18 fps, GDD §8.6 shot 4). Registered by id with
    /// <see cref="FeedCameras"/> while enabled.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(ExecOrder.Cinematics - 5)]
    public sealed class FeedCamera : MonoBehaviour
    {
        /// <summary>Feed render texture width (TECH_PLAN §3.6).</summary>
        public const int Width = 512;
        /// <summary>Feed render texture height.</summary>
        public const int Height = 384;
        /// <summary>Index of PC_Renderer_RT in the URP asset's renderer list (TECH_PLAN §3.5).</summary>
        public const int FeedRendererIndex = 1;

        [SerializeField] private string _id;
        [SerializeField] private Camera _camera;
        [SerializeField, Tooltip("Frames per second (0 = every frame).")] private float _fps;
        [SerializeField] private bool _renderShadows = true;

        private RenderTexture _rt;
        private int _holders;
        private float _accum;
        private float _baseFov = -1f;
        private float _zoomFrom;
        private float _zoomTo;
        private float _zoomTime = -1f;
        private float _zoomSeconds;
        private bool _zoomJerky;

        /// <summary>Feed id (<see cref="BindingIds"/>).</summary>
        public string Id => _id;
        /// <summary>The camera.</summary>
        public Camera Camera => _camera;
        /// <summary>True while at least one consumer holds the feed.</summary>
        public bool InUse => _holders > 0;
        /// <summary>Frame-rate cap (0 = every frame).</summary>
        public float Fps
        {
            get => _fps;
            set => _fps = Mathf.Max(0f, value);
        }

        private void Awake()
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            if (_camera == null) _camera = gameObject.AddComponent<Camera>();
            _camera.enabled = false;
            ConfigureUrp(_camera, _renderShadows);
        }

        private void OnEnable() => FeedCameras.RegisterFeed(this);

        private void OnDisable()
        {
            FeedCameras.UnregisterFeed(this);
            if (_camera != null) _camera.enabled = false;
        }

        private void OnDestroy()
        {
            if (_camera != null) _camera.targetTexture = null;
            if (_rt != null)
            {
                _rt.Release();
                Destroy(_rt);
                _rt = null;
            }
        }

        /// <summary>Sets the id, camera and cap (runtime spawners and builders).</summary>
        public void Setup(string id, Camera cam, float fps, bool renderShadows)
        {
            if (isActiveAndEnabled) FeedCameras.UnregisterFeed(this);
            _id = id;
            _camera = cam;
            _fps = fps;
            _renderShadows = renderShadows;
            if (_camera != null)
            {
                _camera.enabled = false;
                ConfigureUrp(_camera, renderShadows);
            }
            if (isActiveAndEnabled) FeedCameras.RegisterFeed(this);
        }

        /// <summary>Starts rendering (reference counted) and returns the render texture.</summary>
        public RenderTexture Acquire()
        {
            if (_camera == null) return null;
            if (_rt == null)
            {
                _rt = new RenderTexture(Width, Height, 16, RenderTextureFormat.ARGB32)
                {
                    name = "RT_" + _id,
                    useMipMap = false,
                    autoGenerateMips = false,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };
                _rt.Create();
            }
            _camera.targetTexture = _rt;
            _holders++;
            _accum = float.MaxValue; // render on the next frame
            return _rt;
        }

        /// <summary>Releases one hold; the camera stops when nobody holds it.</summary>
        public void Release()
        {
            _holders = Mathf.Max(0, _holders - 1);
            if (_holders == 0 && _camera != null) _camera.enabled = false;
        }

        /// <summary>
        /// Zooms the feed camera to a vertical FOV over seconds; <paramref name="jerky"/> steps it like a hand-cranked
        /// zoom lens (MID-B shot 5 "a jerky zoom"). A negative FOV returns to the authored FOV.
        /// </summary>
        public void ZoomTo(float verticalFov, float seconds, bool jerky)
        {
            if (_camera == null) return;
            if (_baseFov < 0f) _baseFov = _camera.fieldOfView;
            _zoomFrom = _camera.fieldOfView;
            _zoomTo = verticalFov < 0f ? _baseFov : verticalFov;
            _zoomSeconds = Mathf.Max(0.01f, seconds);
            _zoomTime = 0f;
            _zoomJerky = jerky;
        }

        private void TickZoom()
        {
            if (_zoomTime < 0f || _camera == null) return;
            _zoomTime += Time.unscaledDeltaTime;
            var k = Mathf.Clamp01(_zoomTime / _zoomSeconds);
            if (_zoomJerky) k = Mathf.Clamp01(Mathf.Floor(k * 7f) / 7f + Mathf.PerlinNoise(_zoomTime * 6f, 0.3f) * 0.06f);
            _camera.fieldOfView = LensMath.ZoomFov(_zoomFrom, _zoomTo, k);
            if (_zoomTime >= _zoomSeconds)
            {
                _camera.fieldOfView = _zoomTo;
                _zoomTime = -1f;
            }
        }

        private void Update()
        {
            TickZoom();
            if (_holders <= 0 || _camera == null) return;
            if (_fps <= 0f)
            {
                _camera.enabled = true;
                return;
            }
            _accum += Time.unscaledDeltaTime;
            var step = 1f / _fps;
            if (_accum >= step)
            {
                _accum = _accum >= step * 2f ? 0f : _accum - step;
                _camera.enabled = true;
            }
            else
            {
                _camera.enabled = false;
            }
        }

        /// <summary>URP settings of a feed camera: renderer 1, no post, no AA, optional shadows.</summary>
        public static void ConfigureUrp(Camera cam, bool renderShadows)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            data.renderShadows = renderShadows;
            data.requiresDepthOption = CameraOverrideOption.Off;
            data.requiresColorOption = CameraOverrideOption.Off;
            data.SetRenderer(FeedRendererIndex);
        }
    }
}
