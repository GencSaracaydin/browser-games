using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Platform
{
    /// <summary>
    /// <see cref="IDisplayInfo"/> (TECH_PLAN §7.1). The device pixel ratio is the one the engine renders with
    /// (<c>Module.devicePixelRatio</c>, which the CovertOps template caps at 1.5), so <c>InputReader</c> can turn mouse
    /// deltas from device pixels back into CSS pixels (GDD §11 sensitivity formula). Values are re-read twice a second
    /// (the window can move to another screen). Editor: DPR from <see cref="_editorDevicePixelRatio"/>.
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Platform)]
    [DisallowMultipleComponent]
    public sealed class DisplayInfo : MonoBehaviour, IDisplayInfo
    {
        [SerializeField] private PointerLockController _pointerLock;
        [SerializeField, Tooltip("Device pixel ratio assumed in the editor and non-web players.")]
        private float _editorDevicePixelRatio = 1f;
        [SerializeField, Tooltip("Seconds between refreshes of the browser values.")]
        private float _refreshInterval = 0.5f;

        private float _dpr = 1f;
        private bool _inIframe;
        private float _nextRefresh;

        /// <inheritdoc/>
        public float DevicePixelRatio => _dpr;
        /// <inheritdoc/>
        public bool IsPointerLocked => _pointerLock != null ? _pointerLock.IsLocked : Cursor.lockState == CursorLockMode.Locked;
        /// <inheritdoc/>
        public bool InIframe => _inIframe;
        /// <inheritdoc/>
        public Vector2Int CanvasPixelSize => new Vector2Int(Screen.width, Screen.height);

        private void Awake()
        {
            if (_pointerLock == null) _pointerLock = GetComponent<PointerLockController>();
            Refresh();
            GameServices.Register<IDisplayInfo>(this);
            LSLog.Info(LogCategory.Platform, "Display: DPR " + _dpr.ToString("0.##") + ", canvas " + Screen.width + "x" + Screen.height +
                                             (_inIframe ? ", in iframe" : string.Empty));
        }

        private void OnDestroy() => GameServices.Unregister<IDisplayInfo>(this);

        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + _refreshInterval;
            Refresh();
        }

        private void Refresh()
        {
            if (WebNative.Available)
            {
                var d = WebNative.DevicePixelRatio();
                _dpr = d > 0.1f && d < 10f ? d : 1f;
                _inIframe = WebNative.IsInIframe();
            }
            else
            {
                _dpr = Mathf.Clamp(_editorDevicePixelRatio, 0.25f, 4f);
                _inIframe = false;
            }
        }
    }
}
