using System;
using LongShadow.Core;
using LongShadow.Player.Movement;
using UnityEngine;

namespace LongShadow.Player.Bounds
{
    /// <summary>
    /// Soft out-of-bounds handling (TECH_PLAN §7.3 OutOfBoundsWatcher, GDD §17): entering an <see cref="OutOfBoundsVolume"/>
    /// (found by id through <see cref="IMarkerRegistry"/> from <see cref="GameEvents.TriggerEntered"/>) starts a 6 s
    /// countdown shown as <c>RETURN TO THE MISSION AREA 6…</c> through <see cref="IHud.Banner"/> and plays <c>D093</c>
    /// (Hollis); leaving every volume cancels it; at zero <see cref="IHud.FailCard"/> shows
    /// <c>YOU ABANDONED YOUR TEAM</c>, input locks, and after the card the checkpoint reloads
    /// (<see cref="ICheckpointService.Reload"/> without the death insert). Without a checkpoint service (dev scenes) the
    /// player is put back at the last safe grounded position instead. Cancelled on death and reset on every reload.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OutOfBoundsWatcher : MonoBehaviour
    {
        /// <summary>GDD §17 fail card text.</summary>
        public const string FailCardText = "YOU ABANDONED YOUR TEAM";
        /// <summary>GDD §17 / TECH_PLAN §6.2 banner prefix; the seconds and an ellipsis follow.</summary>
        public const string BannerPrefix = "RETURN TO THE MISSION AREA ";

        [SerializeField] private Collider _playerCollider;
        [SerializeField] private PlayerMotor _motor;
        [SerializeField, Tooltip("Seconds the fail card shows before the reload (unscaled).")]
        private float _failCardSeconds = 2.5f;
        [SerializeField, Tooltip("D093 plays at most once per this many seconds.")]
        private float _lineCooldown = 15f;
        [SerializeField, Tooltip("Safety: input unlocks after this many seconds even if no reload event arrives.")]
        private float _lockTimeout = 12f;

        private readonly OutOfBoundsCountdown _countdown = new OutOfBoundsCountdown();
        private string[] _banners;
        private ServiceRef<IHud> _hud;
        private ServiceRef<IVOService> _vo;
        private ServiceRef<ICheckpointService> _checkpoints;
        private ServiceRef<IInputLock> _locks;
        private ServiceRef<IPlayerHealth> _health;
        private float _lastLineTime = -1000f;
        private float _failTimer = -1f;
        private float _lockTimer = -1f;
        private int _lockHandle;
        private bool _locked;
        private Vector3 _safePosition;
        private float _safeYaw;
        private bool _hasSafe;
        private float _safeSampleTimer;

        /// <summary>Raised when a countdown starts.</summary>
        public event Action CountdownStarted;
        /// <summary>Raised when the player returns in time.</summary>
        public event Action CountdownCancelled;
        /// <summary>Raised when the countdown expires (fail card shown).</summary>
        public event Action Failed;

        /// <summary>True while counting down.</summary>
        public bool IsCounting => _countdown.IsCounting;
        /// <summary>Whole seconds left.</summary>
        public int SecondsLeft => _countdown.SecondsLeft;
        /// <summary>True between the fail card and the reload.</summary>
        public bool IsFailing => _failTimer >= 0f;
        /// <summary>The rules object (tests).</summary>
        public OutOfBoundsCountdown Countdown => _countdown;
        /// <summary>Last safe grounded position outside every OOB volume (dev fallback for the reload).</summary>
        public Vector3 SafePosition => _safePosition;

        /// <summary>Banner text for a number of seconds ("RETURN TO THE MISSION AREA 6…"); cached, allocation-free after warm-up.</summary>
        public string BannerText(int seconds)
        {
            if (_banners == null) _banners = new string[31];
            var i = Mathf.Clamp(seconds, 0, _banners.Length - 1);
            return _banners[i] ?? (_banners[i] = BannerPrefix + i + "…");
        }

        /// <summary>Wires the player collider and motor (PlayerRigFactory).</summary>
        internal void Bind(Collider playerCollider, PlayerMotor motor)
        {
            _playerCollider = playerCollider;
            _motor = motor;
        }

        private void Awake()
        {
            if (_playerCollider == null) _playerCollider = GetComponent<CharacterController>();
            if (_motor == null) _motor = GetComponent<PlayerMotor>();
            _safePosition = transform.position;
            _safeYaw = MathX.BearingOf(transform.forward);
            _hasSafe = true;
        }

        private void OnEnable()
        {
            GameEvents.TriggerEntered += OnTriggerEnteredEvent;
            GameEvents.TriggerExited += OnTriggerExitedEvent;
            GameEvents.CheckpointLoaded += OnCheckpointLoaded;
            GameEvents.PlayerRespawned += OnRespawned;
        }

        private void OnDisable()
        {
            GameEvents.TriggerEntered -= OnTriggerEnteredEvent;
            GameEvents.TriggerExited -= OnTriggerExitedEvent;
            GameEvents.CheckpointLoaded -= OnCheckpointLoaded;
            GameEvents.PlayerRespawned -= OnRespawned;
            ReleaseLock();
            _failTimer = -1f;
        }

        private void Update() => Tick(Time.deltaTime, Time.unscaledDeltaTime);

        /// <summary>Advances the countdown (scaled time) and the fail card (unscaled time).</summary>
        internal void Tick(float dt, float unscaledDt)
        {
            if (_failTimer >= 0f)
            {
                _failTimer -= unscaledDt;
                if (_failTimer < 0f) Reload();
            }
            if (_locked && _lockTimer >= 0f)
            {
                _lockTimer -= unscaledDt;
                if (_lockTimer < 0f) ReleaseLock();
            }

            switch (_countdown.Tick(dt))
            {
                case OutOfBoundsCountdown.Result.SecondChanged:
                    _hud.Value?.Banner(BannerText(_countdown.SecondsLeft), 1.15f);
                    break;
                case OutOfBoundsCountdown.Result.Expired:
                    Fail();
                    break;
            }

            if (!_countdown.IsCounting && _countdown.InsideCount == 0 && _failTimer < 0f) SampleSafePosition(dt);
        }

        /// <summary>Enters an OOB volume by id (also used by tests and the dev console).</summary>
        public void EnterVolume(string id, float seconds)
        {
            if (_failTimer >= 0f || !Alive()) return;
            if (!_countdown.Enter(id, seconds)) return;
            LSLog.Info(LogCategory.Player, "Out of bounds: " + id + " (" + seconds.ToString("0") + " s countdown)", this);
            var vo = _vo.Value;
            if (vo != null && Time.unscaledTime - _lastLineTime >= _lineCooldown)
            {
                _lastLineTime = Time.unscaledTime;
                vo.Play(PlayerCues.OutOfBoundsLine, new VOOptions { Priority = VOPriority.Objective });
            }
            CountdownStarted?.Invoke();
        }

        /// <summary>Leaves an OOB volume by id.</summary>
        public void ExitVolume(string id)
        {
            if (!_countdown.Exit(id)) return;
            _hud.Value?.Banner(string.Empty, 0f);
            LSLog.Info(LogCategory.Player, "Back inside the mission area", this);
            CountdownCancelled?.Invoke();
        }

        /// <summary>Stops a running countdown (death, cinematics) without failing.</summary>
        public void Cancel()
        {
            if (!_countdown.IsCounting) return;
            _countdown.Cancel();
            _hud.Value?.Banner(string.Empty, 0f);
        }

        /// <summary>Forgets every volume and any pending fail (checkpoint restore).</summary>
        public void ResetState()
        {
            var wasCounting = _countdown.IsCounting;
            _countdown.Reset();
            _failTimer = -1f;
            ReleaseLock();
            if (wasCounting) _hud.Value?.Banner(string.Empty, 0f);
        }

        private void Fail()
        {
            LSLog.Info(LogCategory.Player, "Out of bounds: countdown expired, reloading the checkpoint", this);
            _hud.Value?.FailCard(FailCardText);
            var locks = _locks.Value;
            if (locks != null && !_locked)
            {
                _lockHandle = locks.Lock(InputLockFlags.All, "OutOfBounds");
                _locked = true;
                _lockTimer = _lockTimeout;
            }
            _failTimer = Mathf.Max(0f, _failCardSeconds);
            Failed?.Invoke();
        }

        private void Reload()
        {
            _failTimer = -1f;
            var cp = _checkpoints.Value;
            if (cp != null)
            {
                cp.Reload(false); // GDD §17: the same reload as Restart Checkpoint, without the death insert
                return;
            }
            LSLog.WarnOnce("player-oob-nocp", LogCategory.Player,
                "No ICheckpointService: out-of-bounds reload falls back to the last safe position (dev scenes only).", this);
            if (TryGetComponent(out IPlayerRig rig)) rig.Teleport(_safePosition, _safeYaw);
            else transform.position = _safePosition;
            ResetState();
        }

        private void OnTriggerEnteredEvent(string id, Collider other)
        {
            if (!IsMine(other)) return;
            if (!MarkerRegistry.Instance.TryGet(id, out OutOfBoundsVolume volume)) return;
            EnterVolume(id, volume.CountdownSeconds);
        }

        private void OnTriggerExitedEvent(string id, Collider other)
        {
            if (!IsMine(other)) return;
            ExitVolume(id);
        }

        private void OnCheckpointLoaded(string cpId) => ResetState();

        private void OnRespawned() => ResetState();

        private bool IsMine(Collider c)
        {
            if (c == null) return false;
            if (_playerCollider != null) return c == _playerCollider;
            return TriggerVolume.IsPlayer(c);
        }

        private bool Alive()
        {
            var h = _health.Value;
            return h == null || h.IsAlive;
        }

        private void SampleSafePosition(float dt)
        {
            _safeSampleTimer -= dt;
            if (_safeSampleTimer > 0f) return;
            _safeSampleTimer = 0.5f;
            if (_motor != null && !_motor.IsGrounded) return;
            _safePosition = transform.position;
            _safeYaw = MathX.BearingOf(transform.forward);
            _hasSafe = true;
        }

        private void ReleaseLock()
        {
            if (!_locked) return;
            _locked = false;
            _lockTimer = -1f;
            _locks.Value?.Unlock(_lockHandle);
        }

        /// <summary>True once a safe position was recorded.</summary>
        public bool HasSafePosition => _hasSafe;
    }
}
