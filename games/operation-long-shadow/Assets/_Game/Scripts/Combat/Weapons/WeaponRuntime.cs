using LongShadow.Combat.Ballistics;
using LongShadow.Combat.Data;
using UnityEngine;

namespace LongShadow.Combat.Weapons
{
    /// <summary>
    /// The rules of one held weapon, free of Unity objects so EditMode tests can drive it frame by frame (TECH_PLAN §7.5
    /// PlayerWeapon: fire state, RPM, sprint-out lock, spread, recoil, ADS blend, reload state machine). PlayerWeapon owns
    /// one per carried weapon and turns its decisions into raycasts, effects and rig calls.
    /// <para>Per frame: <see cref="Tick"/> with the input, then <see cref="TakeShot"/> once for every shot it reports.</para>
    /// <para>Rules: automatic weapons keep exact RPM cadence; semi-automatic weapons fire per press, capped at their RPM with a
    /// 0.12 s press buffer; no fire while sprinting or within the sprint-out time (GDD §12: 0.20 s, RPK 0.32 s); ADS blends
    /// linearly over the ADS time (GDD §13.1) and cannot rise while sprinting or reloading; an empty magazine auto-reloads
    /// when the trigger is pressed (or 0.15 s after the last round when the trigger is released) if there is reserve;
    /// sprinting cancels a reload (ammo already seated stays, ReloadStateMachine); the door gun has no ammunition and
    /// stops at its heat limit (GDD §13.3).</para>
    /// </summary>
    public sealed class WeaponRuntime
    {
        /// <summary>Delay after the last round before an empty magazine reloads itself (trigger released).</summary>
        public const float AutoReloadDelay = 0.15f;

        private FireCadence _cadence;
        private SpreadModel _spread;
        private RecoilRecovery _recovery;
        private HeatModel _heat;
        private float _clock;
        private float _sinceShot = 10f;
        private int _burstShots;
        private int _totalShots;
        private bool _adsTarget;
        private WeaponInput _last;

        /// <summary>Creates the runtime of <paramref name="spec"/> with <paramref name="ammo"/> (null for infinite weapons).</summary>
        public WeaponRuntime(WeaponSpec spec, WeaponAmmo ammo, DoorGunSpec heat = null)
        {
            Spec = spec;
            Ammo = ammo;
            HeatSpec = heat;
            Reload = new ReloadStateMachine();
            Reset();
        }

        /// <summary>The weapon's numbers.</summary>
        public WeaponSpec Spec { get; }
        /// <summary>Magazine and reserve (null for infinite-ammo weapons).</summary>
        public WeaponAmmo Ammo { get; private set; }
        /// <summary>Heat rules (door gun) or null.</summary>
        public DoorGunSpec HeatSpec { get; }
        /// <summary>Reload state.</summary>
        public ReloadStateMachine Reload { get; }
        /// <summary>ADS blend 0..1 (linear; visuals smooth it).</summary>
        public float AdsBlend { get; private set; }
        /// <summary>The weapon wants to be aimed (reported to the rig so the motor ends sprinting).</summary>
        public bool AdsIntent { get; private set; }
        /// <summary>Spread cone half-angle for the current state (degrees).</summary>
        public float CurrentSpread { get; private set; }
        /// <summary>Shots fired since the last burst reset.</summary>
        public int BurstShots => _burstShots;
        /// <summary>Rounds fired since equip (tracer cadence).</summary>
        public int TotalShots => _totalShots;
        /// <summary>Seconds since the last shot.</summary>
        public float SinceShot => _sinceShot;
        /// <summary>Internal clock (seconds since creation).</summary>
        public float Clock => _clock;
        /// <summary>Heat 0..1 (door gun).</summary>
        public float Heat01 => HeatSpec != null ? _heat.Heat01(HeatSpec) : 0f;
        /// <summary>True while the door gun is overheated.</summary>
        public bool Overheated => HeatSpec != null && _heat.Overheated;
        /// <summary>Recoil the player has not compensated yet (degrees).</summary>
        public Vector2 RecoilDebt => new Vector2(_recovery.DebtPitch, _recovery.DebtYaw);
        /// <summary>Hip bloom (degrees).</summary>
        public float Bloom => _spread.Bloom;

        /// <summary>Swaps the ammunition object (pickup swap, checkpoint restore).</summary>
        public void SetAmmo(WeaponAmmo ammo) => Ammo = ammo;

        /// <summary>Forgets transient state (equip, checkpoint): bloom, recoil debt, reload, ADS, heat, cadence.</summary>
        public void Reset()
        {
            _cadence.Reset(_clock);
            _spread.Reset();
            _recovery.Reset();
            _heat.Reset();
            Reload.Reset();
            AdsBlend = 0f;
            AdsIntent = false;
            _adsTarget = false;
            _burstShots = 0;
            _sinceShot = 10f;
        }

        /// <summary>Reports the player's own look movement (degrees; pitch up, yaw right) so pulling against recoil pays it down.</summary>
        public void OnPlayerLook(float pitchUp, float yawRight) => _recovery.OnPlayerLook(pitchUp, yawRight);

        /// <summary>Cancels a running reload (switch, grenade, knife, holster); ammo already seated stays.</summary>
        public ReloadEvents CancelReload() => Reload.Cancel();

        /// <summary>Starts a reload if it would add rounds (and nothing blocks it); returns Started or None.</summary>
        public ReloadEvents TryReload(bool blocked, bool sprinting)
        {
            if (blocked || sprinting || Ammo == null || Reload.IsReloading) return ReloadEvents.None;
            return Reload.Begin(Spec, Ammo);
        }

        /// <summary>Advances one frame.</summary>
        public WeaponFrame Tick(float dt, in WeaponInput input)
        {
            var f = default(WeaponFrame);
            if (dt < 0f) dt = 0f;
            _clock += dt;
            _sinceShot += dt;
            _last = input;
            var r = Spec.Recoil;

            // --- Reload: cancel on sprint or block, else advance.
            if (Reload.IsReloading)
            {
                if (input.Sprinting || input.Blocked) f.Reload |= Reload.Cancel();
                else f.Reload |= Reload.Tick(dt, Ammo);
            }
            if (!Reload.IsReloading && Ammo != null && !input.Blocked && !input.Sprinting)
            {
                var wantReload = input.ReloadDown;
                if (Ammo.IsEmpty && Ammo.Reserve > 0)
                {
                    if (input.TriggerDown) wantReload = true;
                    else if (!input.TriggerHeld && _sinceShot >= AutoReloadDelay) wantReload = true;
                }
                if (wantReload)
                {
                    var started = Reload.Begin(Spec, Ammo);
                    f.Reload |= started;
                    if (started != ReloadEvents.None) _burstShots = 0;
                }
            }

            // --- ADS (GDD §13.1 ADS time).
            var canAds = Spec.CanAds && !input.Blocked && !Reload.IsReloading;
            AdsIntent = input.AimHeld && canAds;
            var target = AdsIntent && !input.Sprinting;
            if (target != _adsTarget)
            {
                if (target) f.AdsStarted = true;
                else f.AdsEnded = true;
                _adsTarget = target;
            }
            var adsRate = Spec.AdsTime > 0f ? dt / Spec.AdsTime : 1f;
            AdsBlend = Mathf.MoveTowards(AdsBlend, target ? 1f : 0f, adsRate);

            // --- Heat (door gun).
            if (HeatSpec != null)
            {
                var wasOver = _heat.Overheated;
                _heat.Tick(HeatSpec, dt, input.TriggerHeld && !input.Blocked && !_heat.Overheated);
                if (wasOver && !_heat.Overheated) f.Cooled = true;
            }

            // --- Recoil recovery (GDD §13.1 "Recovery 8°/s after 0.08 s").
            f.Recovery = _recovery.Step(dt, r.RecoveryDelay, r.RecoveryRate);

            // --- Spread state.
            _spread.Tick(Spec.Spread, dt);
            CurrentSpread = _spread.Current(Spec.Spread, AdsBlend, input.Move01, input.Grounded, input.Crouched);

            // --- Fire.
            var canFire = !input.Blocked && !Reload.IsReloading && !input.Sprinting && !(HeatSpec != null && _heat.Overheated);
            var sprintOutLeft = Spec.SprintOutTime - input.SinceSprintEnd;
            if (sprintOutLeft > 0f) _cadence.BlockUntil(_clock + sprintOutLeft);
            if (!canFire)
            {
                if (Spec.IsAutomatic) _cadence.TickAuto(_clock, Spec.ShotInterval, false);
                f.Shots = 0;
            }
            else if (Spec.IsAutomatic)
            {
                f.Shots = _cadence.TickAuto(_clock, Spec.ShotInterval, input.TriggerHeld);
            }
            else
            {
                f.Shots = _cadence.TickSemi(_clock, Spec.ShotInterval, input.TriggerDown);
            }

            if (Ammo != null)
            {
                if (f.Shots > Ammo.Magazine) f.Shots = Ammo.Magazine;
                // Empty with nothing to reload: the hammer falls on an empty chamber.
                if (input.TriggerDown && Ammo.IsEmpty && Ammo.Reserve <= 0 && !input.Blocked && !input.Sprinting) f.DryFire = true;
            }

            // A released trigger ends the burst once the burst reset time passes (the fixed pattern restarts).
            if (!input.TriggerHeld && _sinceShot > r.BurstResetTime) _burstShots = 0;
            return f;
        }

        /// <summary>
        /// Fires one round: consumes ammunition or adds heat, picks the recoil kick for the burst index, samples the spread
        /// cone (before this shot's bloom) and decides the tracer. <paramref name="u1"/>..<paramref name="u4"/> are uniform
        /// randoms in [0, 1).
        /// </summary>
        public ShotSolution TakeShot(float u1, float u2, float u3, float u4)
        {
            var s = default(ShotSolution);
            if (Ammo != null && !Ammo.TryConsume()) return s;
            if (HeatSpec != null) s.Overheated = _heat.OnShot(HeatSpec);
            var r = Spec.Recoil;
            if (_sinceShot > r.BurstResetTime) _burstShots = 0;
            s.BurstIndex = _burstShots;
            s.SpreadDegrees = _spread.Current(Spec.Spread, AdsBlend, _last.Move01, _last.Grounded, _last.Crouched);
            s.SpreadRandom = new Vector2(u3, u4);
            var kick = RecoilPattern.Kick(r, _burstShots, u1, u2);
            s.Kick = RecoilPattern.Scaled(r, kick, AdsBlend);
            _recovery.OnKick(s.Kick.x, s.Kick.y);
            _spread.OnShot(Spec.Spread);
            _burstShots++;
            _totalShots++;
            _sinceShot = 0f;
            var every = Spec.TracerEvery;
            s.Tracer = every > 0 && _totalShots % every == 0;
            s.LastRound = Ammo != null && Ammo.IsEmpty;
            return s;
        }
    }
}
