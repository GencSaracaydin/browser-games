using System;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Platform
{
    /// <summary>
    /// Browser pointer lock (TECH_PLAN §7.1, webgl.md §4.8). Sets <c>WebGLInput.stickyCursorLock = false</c> so Esc
    /// (reserved by the browser) releases the lock and the game pauses; requests the lock with
    /// <c>unadjustedMovement</c> first (raw mouse), falling back to a plain lock; honours Chrome's ~1 s re-lock cooldown
    /// after the user exits; and arms the page so the very next pointerdown/keydown locks inside the user gesture
    /// (a request issued a frame after the click can be refused). Raises <see cref="GameEvents.PointerLockChanged"/>.
    /// In the editor it drives <see cref="Cursor.lockState"/> instead.
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Platform - 10)]
    [DisallowMultipleComponent]
    public sealed class PointerLockController : MonoBehaviour
    {
        [SerializeField, Tooltip("Chrome refuses a new lock for about 1 s after the user exits (webgl.md §4.8).")]
        private float _relockCooldown = 1.1f;
        [SerializeField, Tooltip("Ask for raw mouse input (requestPointerLock({unadjustedMovement:true})) first.")]
        private bool _unadjustedMovement = true;
        [SerializeField, Tooltip("Editor: lock the cursor in play mode like the browser does.")]
        private bool _lockInEditor = true;

        private bool _locked;
        private bool _wanted;
        private bool _hasLockedOnce;
        private float _lastUnlock = -10f;
        private float _retryAt = -1f;
        private string _lastError = string.Empty;
        private bool _disabledByLaunch;

        /// <summary>Raised after the lock state changed (true = locked).</summary>
        public event Action<bool> LockChanged;
        /// <summary>Raised when a lock request was refused (DOMException name).</summary>
        public event Action<string> LockRefused;

        /// <summary>True while the pointer is locked to the canvas.</summary>
        public bool IsLocked => _locked;
        /// <summary>True between <see cref="RequestLock"/> and <see cref="ReleaseLock"/>.</summary>
        public bool IsWanted => _wanted;
        /// <summary>True once a lock was granted this session (a refused first lock means "click to look" mode).</summary>
        public bool HasLockedOnce => _hasLockedOnce;
        /// <summary>True while a new request would hit the browser's re-lock cooldown.</summary>
        public bool InCooldown => !_locked && Time.unscaledTime - _lastUnlock < _relockCooldown;
        /// <summary>Seconds left in the cooldown (0 when none).</summary>
        public float CooldownRemaining => _locked ? 0f : Mathf.Max(0f, _relockCooldown - (Time.unscaledTime - _lastUnlock));
        /// <summary>Last refusal reason ("" if none).</summary>
        public string LastError => _lastError;
        /// <summary>False in the automation pane with ?nolock=1.</summary>
        public bool Enabled => !_disabledByLaunch;

        private void Awake()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLInput.stickyCursorLock = false;
#endif
            _disabledByLaunch = LaunchOptions.Current.NoLock;
            _locked = ReadNativeLocked();
        }

        private void OnEnable()
        {
            WebBridge.PointerLockChanged += OnNativeLockChanged;
            WebBridge.PointerLockError += OnNativeLockError;
        }

        private void OnDisable()
        {
            WebBridge.PointerLockChanged -= OnNativeLockChanged;
            WebBridge.PointerLockError -= OnNativeLockError;
        }

        /// <summary>
        /// Asks for the lock: immediately when allowed (call it from a click handler when you can), and again on the
        /// page's next pointerdown/keydown if the browser refuses or the cooldown is running.
        /// </summary>
        public void RequestLock()
        {
            if (_disabledByLaunch) return;
            _wanted = true;
            if (_locked) return;
            if (WebNative.Available)
            {
                WebNative.ArmGesture(WebNative.GestureLock, _unadjustedMovement);
                if (InCooldown) _retryAt = _lastUnlock + _relockCooldown;
                else WebNative.RequestPointerLock(_unadjustedMovement);
            }
            else if (_lockInEditor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                SetLocked(Cursor.lockState == CursorLockMode.Locked);
            }
        }

        /// <summary>
        /// Marks the lock as wanted without issuing a new request now: the page already requested it inside the
        /// browser gesture (HTML click gate, or the gesture armed for WP08's in-engine gate), and a second request
        /// while the first is pending would only be refused. The page stays armed, so if the browser refused, the
        /// next click locks. In the editor this is the same as <see cref="RequestLock"/>.
        /// </summary>
        public void ExpectLock()
        {
            if (_disabledByLaunch) return;
            if (!WebNative.Available)
            {
                RequestLock();
                return;
            }
            _wanted = true;
            if (ReadNativeLocked())
            {
                SetLocked(true); // the pointerlockchange event may have fired before anyone listened; no-op if known
                return;
            }
            WebNative.ArmGesture(WebNative.GestureLock, _unadjustedMovement);
        }

        /// <summary>Stops wanting the lock and releases it (pause menus, debrief).</summary>
        public void ReleaseLock()
        {
            _wanted = false;
            _retryAt = -1f;
            if (WebNative.Available)
            {
                WebNative.ArmGesture(0, _unadjustedMovement);
                if (_locked) WebNative.ExitPointerLock();
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                SetLocked(false);
            }
        }

        /// <summary>Clears the "wanted" flag without touching the browser (the lock was lost and the game paused).</summary>
        public void Disarm()
        {
            _wanted = false;
            _retryAt = -1f;
            if (WebNative.Available) WebNative.ArmGesture(0, _unadjustedMovement);
        }

        private void Update()
        {
            if (_retryAt >= 0f && Time.unscaledTime >= _retryAt)
            {
                _retryAt = -1f;
                if (_wanted && !_locked && WebNative.Available) WebNative.RequestPointerLock(_unadjustedMovement);
            }
            if (!WebNative.Available)
            {
                // Editor: Esc releases the cursor without telling us.
                var nowLocked = Cursor.lockState == CursorLockMode.Locked;
                if (nowLocked != _locked) SetLocked(nowLocked);
            }
        }

        private void OnNativeLockChanged(bool locked) => SetLocked(locked);

        private void OnNativeLockError(string error)
        {
            _lastError = error ?? string.Empty;
            LSLog.WarnOnce("plock-refused:" + _lastError, LogCategory.Platform,
                "Pointer lock refused (" + _lastError + "); the next click will try again. Mouse look still works while the cursor is over the canvas.");
            LockRefused?.Invoke(_lastError);
        }

        private void SetLocked(bool locked)
        {
            if (locked == _locked) return;
            _locked = locked;
            if (locked)
            {
                _hasLockedOnce = true;
                _retryAt = -1f;
                if (WebNative.Available) WebNative.ArmGesture(0, _unadjustedMovement);
            }
            else
            {
                _lastUnlock = Time.unscaledTime;
            }
            LSLog.Verbose(LogCategory.Platform, "Pointer lock " + (locked ? "on" : "off"));
            GameEvents.RaisePointerLockChanged(locked);
            LockChanged?.Invoke(locked);
        }

        private static bool ReadNativeLocked() =>
            WebNative.Available ? WebNative.IsPointerLocked() : Cursor.lockState == CursorLockMode.Locked;
    }
}
