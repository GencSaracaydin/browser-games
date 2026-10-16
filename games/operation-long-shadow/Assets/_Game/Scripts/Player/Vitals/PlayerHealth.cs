using System;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Player.Vitals
{
    /// <summary>
    /// Player health (TECH_PLAN §7.3/§7.4, GDD §16): <see cref="IPlayerHealth"/> service and <see cref="ISaveable"/>
    /// ("Player.Health"; a checkpoint restore always returns full health, GDD §17). Applies the difficulty scaling of
    /// <see cref="PlayerDamageMath"/>, the <see cref="PlayerHealthModel"/> rules (regen, last stand, Beat 11 modifiers),
    /// raises <see cref="GameEvents.PlayerDamaged"/>/<see cref="GameEvents.PlayerDied"/>/<see cref="GameEvents.PlayerRespawned"/>,
    /// and drives the health feedback of GDD §11: <see cref="IScreenFX.SetLowHealth"/> below 50 %, heartbeat loop and
    /// muffled hearing (ducking Effects and Ambience) below 25 %. Every service is optional.
    /// <para>Damage absorbed by last-stand immunity still raises PlayerDamaged with amount 0 so the HUD shows the
    /// direction. PlayerRespawned is raised here (checkpoint restore, or ResetFull after death); the checkpoint service
    /// does not need to raise it.</para>
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Player)]
    [DisallowMultipleComponent]
    public sealed class PlayerHealth : MonoBehaviour, IPlayerHealth, ISaveable
    {
        /// <summary>Save key in checkpoint snapshots.</summary>
        public const string Key = "Player.Health";

        [SerializeField, Tooltip("Chest point AI aims at (created at 1.3 m if empty).")]
        private Transform _aimPoint;
        [SerializeField] private bool _registerService = true;
        [SerializeField, Tooltip("GDD §11: desaturation and vignette below 50 % HP.")]
        private float _lowHealthFraction = 0.5f;
        [SerializeField, Tooltip("GDD §11: heartbeat and muffled hearing below 25 % HP.")]
        private float _criticalFraction = 0.25f;
        [SerializeField, Tooltip("Muffle depth on Effects and Ambience while critical (dB).")]
        private float _muffleDb = -9f;

        private PlayerHealthModel _model;
        private Difficulty? _difficultyOverride;
        private ServiceRef<ISettingsService> _settings;
        private ServiceRef<IScreenFX> _screenFx;
        private ServiceRef<IAudioService> _audio;
        private ICheckpointService _registeredWith;
        private int _heartbeatHandle;
        private bool _heartbeatOn;
        private float _muffleTimer;
        private float _lastLowHealth = -1f;
        private bool _registered;

        /// <summary>Raised after damage with the HP actually removed (0 while immune).</summary>
        public event Action<DamageInfo> Damaged;
        /// <summary>Raised once on death.</summary>
        public event Action<DamageInfo> Died;
        /// <summary>Raised when the player comes back (checkpoint restore, or a reset after death).</summary>
        public event Action Respawned;

        private PlayerHealthModel Model => _model ?? (_model = new PlayerHealthModel(CurrentProfile()));

        /// <summary>The rules object (tests, telemetry).</summary>
        public PlayerHealthModel Rules => Model;
        /// <inheritdoc/>
        public float Current => Model.Current;
        /// <inheritdoc/>
        public float Max => Model.Max;
        /// <summary>Current / Max.</summary>
        public float Normalized => Model.Normalized;
        /// <inheritdoc/>
        public float TimeSinceDamage => Model.TimeSinceDamage;
        /// <inheritdoc/>
        public bool GodMode { get => Model.GodMode; set => Model.GodMode = value; }
        /// <inheritdoc/>
        public Team Team => Team.Player;
        /// <inheritdoc/>
        public bool IsAlive => Model.IsAlive;
        /// <inheritdoc/>
        public Transform AimPoint
        {
            get
            {
                if (_aimPoint == null)
                {
                    _aimPoint = new GameObject(PlayerRigNames.AimPoint).transform;
                    _aimPoint.SetParent(transform, false);
                    _aimPoint.localPosition = new Vector3(0f, 1.3f, 0f);
                }
                return _aimPoint;
            }
        }
        /// <summary>Difficulty in effect.</summary>
        public DifficultyProfile Profile => Model.Profile;

        /// <summary>Wires the aim point (PlayerRigFactory).</summary>
        internal void Bind(Transform aimPoint) => _aimPoint = aimPoint;

        private void Awake()
        {
            if (_registerService)
            {
                GameServices.Register<IPlayerHealth>(this);
                _registered = true;
            }
        }

        private void OnEnable() => GameServices.Changed += OnServiceChanged;

        private void Start() => TryRegisterCheckpoint();

        private void OnDisable()
        {
            GameServices.Changed -= OnServiceChanged;
            StopHeartbeat(0.3f);
        }

        private void OnDestroy()
        {
            if (_registered) GameServices.Unregister<IPlayerHealth>(this);
            if (_registeredWith != null && !(_registeredWith is UnityEngine.Object o && o == null)) _registeredWith.Unregister(this);
            _registeredWith = null;
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            var m = Model;
            m.Profile = CurrentProfile();
            m.Tick(dt);
            UpdateFeedback(dt);
        }

        /// <inheritdoc/>
        public void ApplyDamage(in DamageInfo d)
        {
            var m = Model;
            if (!m.IsAlive || m.GodMode) return;
            m.Profile = CurrentProfile();
            var scaled = PlayerDamageMath.ScaleIncoming(in d, m.Profile);
            if (scaled <= 0f) return;
            var applied = m.ApplyDamage(scaled);
            var final = d.WithAmount(applied);
            Damaged?.Invoke(final);
            GameEvents.RaisePlayerDamaged(final);
            if (m.IsAlive) return;
            StopHeartbeat(0.2f);
            PushLowHealth(1f);
            LSLog.Info(LogCategory.Player, "Player died (" + d.Type + (d.WeaponId != null ? " " + d.WeaponId : "") + ")", this);
            Died?.Invoke(final);
            GameEvents.RaisePlayerDied(final);
        }

        /// <inheritdoc/>
        public void ResetFull() => ResetFull(false);

        /// <summary>Full health; raises PlayerRespawned when the player was dead or when <paramref name="checkpointRestore"/>.</summary>
        public void ResetFull(bool checkpointRestore)
        {
            var wasDead = !Model.IsAlive;
            Model.ResetFull();
            StopHeartbeat(0.3f);
            PushLowHealth(0f);
            if (!wasDead && !checkpointRestore) return;
            Respawned?.Invoke();
            GameEvents.RaisePlayerRespawned();
        }

        /// <summary>Beat 11 damage modifiers (scale 0.25, floor 1); (1, 0) restores normal damage.</summary>
        public void SetDamageModifiers(float scale, float floorHp)
        {
            Model.DamageScale = Mathf.Max(0f, scale);
            Model.FloorHp = Mathf.Max(0f, floorHp);
        }

        /// <summary>Dev: forces a difficulty regardless of the settings service; null releases.</summary>
        public void SetDifficultyOverride(Difficulty? level)
        {
            _difficultyOverride = level;
            Model.Profile = CurrentProfile();
        }

        /// <summary>Dev: sets HP directly.</summary>
        public void SetCurrent(float hp) => Model.SetCurrent(hp);

        /// <inheritdoc/>
        public string SaveKey => Key;

        /// <inheritdoc/>
        public void Capture(SaveWriter w)
        {
            w.Write("hp", Model.Current);
            w.Write("alive", Model.IsAlive);
        }

        /// <inheritdoc/>
        public void Restore(SaveReader r) => ResetFull(true); // GDD §17: every reload is at full health

        private DifficultyProfile CurrentProfile()
        {
            if (_difficultyOverride.HasValue) return DifficultyProfile.For(_difficultyOverride.Value);
            var s = _settings.Value;
            if (s != null)
            {
                var p = s.Difficulty;
                if (p.PlayerHp > 0f) return p;
            }
            return DifficultyProfile.For(Difficulty.Regular);
        }

        private void UpdateFeedback(float dt)
        {
            var m = Model;
            var n = m.Normalized;
            var low = !m.IsAlive ? 1f : (_lowHealthFraction > 0f ? Mathf.Clamp01((_lowHealthFraction - n) / _lowHealthFraction) : 0f);
            PushLowHealth(low);

            var critical = m.IsAlive && n < _criticalFraction;
            if (critical && !_heartbeatOn) StartHeartbeat();
            else if (!critical && _heartbeatOn) StopHeartbeat(1f);
            if (!critical) return;
            _muffleTimer -= dt;
            if (_muffleTimer > 0f) return;
            _muffleTimer = 0.5f;
            var audio = _audio.Value;
            if (audio == null) return;
            audio.Duck(AudioCategories.Effects, _muffleDb, 0.6f);
            audio.Duck(AudioCategories.Ambience, _muffleDb, 0.6f);
        }

        private void PushLowHealth(float value)
        {
            if (Mathf.Abs(value - _lastLowHealth) < 0.004f) return;
            var fx = _screenFx.Value;
            if (fx == null) return;
            fx.SetLowHealth(value);
            _lastLowHealth = value;
        }

        private void StartHeartbeat()
        {
            var audio = _audio.Value;
            if (audio == null) return;
            _heartbeatHandle = audio.PlayLoop(PlayerCues.Heartbeat, null);
            _heartbeatOn = true;
        }

        private void StopHeartbeat(float fade)
        {
            if (!_heartbeatOn) return;
            _heartbeatOn = false;
            _audio.Value?.StopLoop(_heartbeatHandle, fade);
        }

        private void OnServiceChanged(Type type, object instance)
        {
            if (type == typeof(ICheckpointService)) TryRegisterCheckpoint();
        }

        private void TryRegisterCheckpoint()
        {
            if (!GameServices.TryGet(out ICheckpointService cp) || ReferenceEquals(cp, _registeredWith)) return;
            cp.Register(this);
            _registeredWith = cp;
        }
    }
}
