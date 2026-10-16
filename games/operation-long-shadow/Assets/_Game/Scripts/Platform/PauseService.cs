using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Platform
{
    /// <summary>
    /// <see cref="IPauseService"/> (TECH_PLAN §5.4, GDD §11 pause menu). Pausing pushes time scale 0 at
    /// <see cref="TimePriority.Pause"/>, sets <c>AudioListener.pause</c> (UI and insert sources use
    /// <c>ignoreListenerPause</c>), locks every input channel and raises <see cref="GameEvents.PauseChanged"/>; WP08's
    /// pause menu listens. Triggers: losing pointer lock (Esc in the browser), the Pause action (Esc in the editor or
    /// in "click to look" mode), and the page becoming hidden. <see cref="Resume"/> must be called from a click (the
    /// menu's RESUME): it unpauses and re-requests the lock, which the browser may delay by ~1 s (Chrome's cooldown);
    /// the page then locks on the next click.
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Platform)]
    [DisallowMultipleComponent]
    public sealed class PauseService : MonoBehaviour, IPauseService
    {
        [SerializeField] private PointerLockController _pointerLock;
        [SerializeField, Tooltip("Pause when the browser releases pointer lock (Esc).")]
        private bool _pauseOnLockLoss = true;
        [SerializeField, Tooltip("Pause when the tab is hidden.")]
        private bool _pauseWhenHidden = true;
        [SerializeField, Tooltip("Ignore a Pause key press this soon after a lock-loss pause (one Esc must not pause twice).")]
        private float _escDebounce = 0.3f;

        private ServiceRef<ITimeScaleService> _time;
        private ServiceRef<IInputLock> _locks;
        private ServiceRef<IInputReader> _input;
        private bool _paused;
        private bool _canPause;
        private int _timeHandle;
        private int _lockHandle;
        private float _pausedAt = -10f;
        private float _resumedAt = -10f;

        /// <inheritdoc/>
        public bool IsPaused => _paused;

        /// <summary>
        /// Seconds until the browser accepts a new pointer lock (Chrome refuses one for ~1 s after the user pressed Esc;
        /// GDD §11 pause menu "Click to resume"). 0 when a click would lock now. Requested for IPauseService in
        /// Docs/CORE_CHANGES.md; until then WP08 can read it through Integration.
        /// </summary>
        public float RelockCooldown => _pointerLock != null ? _pointerLock.CooldownRemaining : 0f;

        /// <inheritdoc/>
        public bool CanPause
        {
            get => _canPause;
            set
            {
                if (_canPause == value) return;
                _canPause = value;
                if (!value && _paused) SetPaused(false, false);
            }
        }

        private void Awake()
        {
            if (_pointerLock == null) _pointerLock = GetComponent<PointerLockController>();
            if (_pointerLock == null) _pointerLock = FindAnyObjectByType<PointerLockController>();
            GameServices.Register<IPauseService>(this);
        }

        private void OnEnable()
        {
            if (_pointerLock != null) _pointerLock.LockChanged += OnLockChanged;
            WebBridge.VisibilityChanged += OnVisibilityChanged;
        }

        private void OnDisable()
        {
            if (_pointerLock != null) _pointerLock.LockChanged -= OnLockChanged;
            WebBridge.VisibilityChanged -= OnVisibilityChanged;
        }

        private void OnDestroy()
        {
            if (_paused) SetPaused(false, false);
            GameServices.Unregister<IPauseService>(this);
        }

        /// <inheritdoc/>
        public void Pause()
        {
            if (!_canPause || _paused) return;
            SetPaused(true, false);
        }

        /// <inheritdoc/>
        public void Resume()
        {
            if (!_paused) return;
            SetPaused(false, true);
        }

        private void Update()
        {
            if (!_canPause) return;
            var input = _input.Value;
            if (input == null || !input.PauseDown) return;
            if (_paused) return; // WP08's menu handles Esc while paused (back/close); resuming needs a click.
            if (Time.unscaledTime - _resumedAt < _escDebounce) return;
            Pause();
        }

        private void OnLockChanged(bool locked)
        {
            if (locked || !_pauseOnLockLoss || !_canPause || _paused) return;
            if (_pointerLock != null && !_pointerLock.IsWanted) return; // released on purpose (menus, cinematics)
            Pause();
        }

        private void OnVisibilityChanged(bool visible)
        {
            if (!visible && _pauseWhenHidden) Pause();
        }

        private void SetPaused(bool paused, bool requestLock)
        {
            _paused = paused;
            var time = _time.Value;
            var locks = _locks.Value;
            if (paused)
            {
                _pausedAt = Time.unscaledTime;
                if (time != null) _timeHandle = time.Push(0f, TimePriority.Pause, "pause");
                if (locks != null) _lockHandle = locks.Lock(InputLockFlags.All, "pause");
                AudioListener.pause = true;
                if (_pointerLock != null)
                {
                    if (_pointerLock.IsLocked) _pointerLock.ReleaseLock();
                    else _pointerLock.Disarm();
                }
            }
            else
            {
                _resumedAt = Time.unscaledTime;
                if (time != null && _timeHandle != 0) time.Pop(_timeHandle);
                if (locks != null && _lockHandle != 0) locks.Unlock(_lockHandle);
                _timeHandle = 0;
                _lockHandle = 0;
                AudioListener.pause = false;
                if (requestLock && _pointerLock != null) _pointerLock.RequestLock();
            }
            LSLog.Info(LogCategory.Platform, paused ? "Paused" : "Resumed (paused " + (Time.unscaledTime - _pausedAt).ToString("0.0") + " s)");
            GameEvents.RaisePauseChanged(paused);
        }
    }
}
