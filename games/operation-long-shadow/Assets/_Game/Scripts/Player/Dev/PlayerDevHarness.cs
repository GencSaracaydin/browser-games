using System;
using LongShadow.Core;
using LongShadow.Player.Movement;
using LongShadow.Player.Vitals;
using UnityEngine;

namespace LongShadow.Player.Dev
{
    /// <summary>
    /// Dev-scene driver for the WP02 movement test course (<c>Dev_PlayerCourse</c>, built by
    /// <c>LongShadow/Player/Build Movement Test Course</c>). It reacts to course pads, which are
    /// <see cref="TriggerVolume"/>s whose ids start with "DEV.":
    /// <list type="bullet">
    /// <item><c>DEV.Gate.A</c> / <c>DEV.Gate.B</c>: timed speed gates; crossing both logs the average speed against the
    /// GDD §12 speed of the stance held (<see cref="MovementTelemetry"/>).</item>
    /// <item><c>DEV.Damage.*</c>: 20 HP from a Legion AK, direction from the pad's forward (damage direction, regen,
    /// last stand).</item>
    /// <item><c>DEV.Kill.*</c>: lethal damage (death flow); the player respawns at <c>DEV.Spawn</c> after 2.5 s.</item>
    /// <item><c>DEV.Difficulty.*</c>: cycles Recruit → Regular → Hardened → Veteran (GDD §16) on the player health.</item>
    /// <item><c>DEV.Binoculars.*</c>: toggles the Beat 1 binoculars.</item>
    /// <item><c>DEV.ForcedWalk.*</c>: MID-A forced walk at 1.2 m/s while inside (GDD §8.5).</item>
    /// <item><c>DEV.Mount.*</c>: mounts the player on the pad's "Seat" child; the next entry dismounts.</item>
    /// </list>
    /// Without a pause service it also locks the cursor on click and unlocks it on Esc, so mouse look works in the editor.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerDevHarness : MonoBehaviour
    {
        /// <summary>Prefix of every course pad id.</summary>
        public const string Prefix = "DEV.";
        /// <summary>Spawn marker id.</summary>
        public const string SpawnId = "DEV.Spawn";
        /// <summary>First speed gate id.</summary>
        public const string GateA = "DEV.Gate.A";
        /// <summary>Second speed gate id.</summary>
        public const string GateB = "DEV.Gate.B";

        [SerializeField] private PlayerRig _rig;
        [SerializeField] private MovementTelemetry _telemetry;
        [SerializeField, Tooltip("GDD §14.1: Legion AK 20 per hit on Regular.")]
        private float _padDamage = 20f;
        [SerializeField] private float _respawnDelay = 2.5f;
        [SerializeField, Tooltip("MID-A forced walk speed (GDD §8.5).")]
        private float _forcedWalkSpeed = 1.2f;
        [SerializeField] private bool _manageCursor = true;

        private ServiceRef<IInputReader> _input;
        private ServiceRef<IPauseService> _pause;
        private Difficulty _difficulty = Difficulty.Regular;
        private float _respawnTimer = -1f;
        private string _gateFrom;
        private float _gateStartTime;
        private Stance _gateStance;
        private bool _gateStanceChanged;

        private void Awake()
        {
            if (_rig == null) _rig = GetComponent<PlayerRig>();
            if (_telemetry == null) _telemetry = GetComponent<MovementTelemetry>();
        }

        private void OnEnable()
        {
            GameEvents.TriggerEntered += OnEntered;
            GameEvents.TriggerExited += OnExited;
            GameEvents.PlayerDied += OnDied;
            GameEvents.MissionEvent += OnMissionEvent;
        }

        private void OnDisable()
        {
            GameEvents.TriggerEntered -= OnEntered;
            GameEvents.TriggerExited -= OnExited;
            GameEvents.PlayerDied -= OnDied;
            GameEvents.MissionEvent -= OnMissionEvent;
        }

        private void Start()
        {
            if (_rig != null && _rig.Health != null) _rig.Health.SetDifficultyOverride(_difficulty);
            LSLog.Info(LogCategory.Player, "Dev course ready: click to capture the mouse; difficulty " + _difficulty, this);
        }

        private void Update()
        {
            if (_manageCursor && _pause.Value == null)
            {
                var input = _input.Value;
                if (input != null)
                {
                    if (Cursor.lockState != CursorLockMode.Locked && input.FireDown)
                    {
                        Cursor.lockState = CursorLockMode.Locked;
                        Cursor.visible = false;
                    }
                    else if (input.PauseDown)
                    {
                        Cursor.lockState = CursorLockMode.None;
                        Cursor.visible = true;
                    }
                }
            }

            if (_gateFrom != null && _rig != null && _rig.Stance != _gateStance) _gateStanceChanged = true;

            if (_respawnTimer >= 0f)
            {
                _respawnTimer -= Time.unscaledDeltaTime;
                if (_respawnTimer < 0f) Respawn();
            }
        }

        private void OnEntered(string id, Collider other)
        {
            if (_rig == null || string.IsNullOrEmpty(id) || !id.StartsWith(Prefix, StringComparison.Ordinal) || !TriggerVolume.IsPlayer(other)) return;
            if (id == GateA || id == GateB) Gate(id);
            else if (id.StartsWith("DEV.Damage", StringComparison.Ordinal)) Damage(id, _padDamage);
            else if (id.StartsWith("DEV.Kill", StringComparison.Ordinal)) Damage(id, 1000f);
            else if (id.StartsWith("DEV.Difficulty", StringComparison.Ordinal)) CycleDifficulty();
            else if (id.StartsWith("DEV.Binoculars", StringComparison.Ordinal)) ToggleBinoculars();
            else if (id.StartsWith("DEV.ForcedWalk", StringComparison.Ordinal)) _rig.SetForcedWalk(_forcedWalkSpeed);
            else if (id.StartsWith("DEV.Mount", StringComparison.Ordinal)) ToggleMount(id);
        }

        private void OnExited(string id, Collider other)
        {
            if (_rig == null || string.IsNullOrEmpty(id) || !TriggerVolume.IsPlayer(other)) return;
            if (id.StartsWith("DEV.ForcedWalk", StringComparison.Ordinal)) _rig.SetForcedWalk(null);
        }

        private void Gate(string id)
        {
            if (_gateFrom == null || _gateFrom == id)
            {
                _gateFrom = id;
                _gateStartTime = Time.time;
                _gateStance = _rig.Stance;
                _gateStanceChanged = false;
                return;
            }
            var registry = MarkerRegistry.Instance;
            var from = registry.Get(_gateFrom);
            var to = registry.Get(id);
            var elapsed = Time.time - _gateStartTime;
            var stance = _gateStance;
            var changed = _gateStanceChanged;
            _gateFrom = null;
            if (from == null || to == null || elapsed <= 0f) return;
            var a = from.position;
            var b = to.position;
            a.y = 0f;
            b.y = 0f;
            var speed = Vector3.Distance(a, b) / elapsed;
            if (changed)
            {
                LSLog.Info(LogCategory.Player, "Speed gate: stance changed between the gates (" + speed.ToString("0.00") + " m/s); hold one stance for a measurement.", this);
                return;
            }
            var motor = _rig.Motor;
            var target = MovementMath.GroundSpeed(motor.Settings, stance, _rig.AdsBlend, motor.SpeedMultiplier, motor.ForcedWalkSpeed);
            if (_telemetry != null) _telemetry.Report("Gate." + stance, speed, target);
            else LSLog.Info(LogCategory.Player, "Speed gate " + stance + ": " + speed.ToString("0.00") + " m/s (target " + target.ToString("0.00") + ")", this);
        }

        private void Damage(string padId, float amount)
        {
            var registry = MarkerRegistry.Instance;
            var pad = registry.Get(padId);
            // The pad's forward is the direction the shot travels (DamageInfo.Direction: instigator towards victim). Like
            // WP04's AI, the pad sends the AK hit already scaled by the difficulty in effect (PlayerDamageMath.AiBulletDamage).
            var dir = pad != null ? pad.forward : -transform.forward;
            var profile = _rig.Health != null ? _rig.Health.Profile : DifficultyProfile.For(_difficulty);
            var scaled = PlayerDamageMath.AiBulletDamage(amount, WeaponIds.AK, profile);
            var d = new DamageInfo(scaled, DamageType.Bullet, HitZone.Torso, transform.position + Vector3.up * 1.3f, dir, null, Team.Legion, WeaponIds.AK);
            _rig.ApplyDamage(in d);
            if (_rig.Health != null)
                LSLog.Info(LogCategory.Player, "Dev damage " + scaled.ToString("0") + " -> " + _rig.Health.Current.ToString("0") + " HP (" + _difficulty + ")", this);
        }

        private void CycleDifficulty()
        {
            _difficulty = (Difficulty)(((int)_difficulty + 1) % 4);
            if (_rig.Health != null)
            {
                _rig.Health.SetDifficultyOverride(_difficulty);
                var p = _rig.Health.Profile;
                LSLog.Info(LogCategory.Player, "Difficulty " + _difficulty + ": regen after " + p.RegenDelay + " s at " + p.RegenRate +
                                               " HP/s, last stand " + p.LastStandImmunity + " s, enemy damage x" + p.EnemyDamageMultiplier, this);
            }
        }

        private void ToggleBinoculars()
        {
            var b = _rig.BinocularsComponent;
            var on = b == null || !b.Available;
            _rig.SetBinocularsAvailable(on);
            LSLog.Info(LogCategory.Player, "Binoculars " + (on ? "available: hold RMB to glass" : "put away"), this);
        }

        private void ToggleMount(string padId)
        {
            if (_rig.MountComponent != null && _rig.MountComponent.IsMounted)
            {
                _rig.Dismount();
                return;
            }
            var pad = MarkerRegistry.Instance.Get(padId);
            var seat = pad != null ? pad.Find("Seat") : null;
            if (seat != null) _rig.Mount(seat);
        }

        private void OnMissionEvent(string id)
        {
            if (id != null && id.StartsWith(PlayerEvents.GlassedPrefix, StringComparison.Ordinal))
                LSLog.Info(LogCategory.Player, "Glassing POI ticked: " + id, this);
        }

        private void OnDied(DamageInfo d) => _respawnTimer = _respawnDelay;

        private void Respawn()
        {
            _respawnTimer = -1f;
            if (_rig == null) return;
            var spawn = MarkerRegistry.Instance.Get(SpawnId);
            if (spawn != null) _rig.Teleport(spawn.position, MathX.BearingOf(spawn.forward));
            if (_rig.Health != null) _rig.Health.ResetFull();
        }
    }
}
