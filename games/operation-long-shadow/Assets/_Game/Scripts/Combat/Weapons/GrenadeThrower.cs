using LongShadow.Combat.Ballistics;
using LongShadow.Combat.Data;
using UnityEngine;

namespace LongShadow.Combat.Weapons
{
    /// <summary>
    /// The player's frag throw (TECH_PLAN §7.5 GrenadeThrower, GDD §13.2): G pulls the pin (the 4.0 s fuse starts at the
    /// press), holding G cooks with fuse ticks on the HUD (<see cref="Cook01"/>), releasing G throws (after the pin pull if
    /// released early). The throw speed makes a 45° throw from eye height land at 18 m; the view pitch plus a 12° bias
    /// sets the angle. A grenade cooked to zero explodes in the hand. Pure state machine: PlayerWeapon spawns the projectile
    /// and plays the effects for the events it returns.
    /// </summary>
    public sealed class GrenadeThrower
    {
        /// <summary>Events of one tick.</summary>
        public enum Event
        {
            /// <summary>Nothing.</summary>
            None = 0,
            /// <summary>The grenade leaves the hand now (call <see cref="Release"/> for its fuse).</summary>
            Throw = 1,
            /// <summary>The fuse ran out in the hand.</summary>
            ExplodedInHand = 2,
            /// <summary>The hand is back on the weapon.</summary>
            Finished = 3,
            /// <summary>A whole second of cooking passed (fuse tick sound).</summary>
            Tick = 4,
        }

        private readonly GrenadeSpec _spec;
        private GrenadeCook _cook;
        private bool _releaseQueued;
        private int _lastTick;
        private float _thrownFuse;

        /// <summary>Creates a thrower for <paramref name="spec"/> (WeaponTable.Frag).</summary>
        public GrenadeThrower(GrenadeSpec spec, float eyeHeight = 1.65f)
        {
            _spec = spec ?? WeaponTable.Frag;
            ThrowSpeed = GrenadeBallistics.SpeedForMaxRange(_spec.ThrowRange, eyeHeight, GrenadeBallistics.Gravity);
        }

        /// <summary>Launch speed (m/s) of a throw.</summary>
        public float ThrowSpeed { get; }
        /// <summary>Current phase.</summary>
        public GrenadePhase Phase { get; private set; }
        /// <summary>Seconds into the current phase.</summary>
        public float PhaseTime { get; private set; }
        /// <summary>True while the hand is busy with a grenade.</summary>
        public bool Active => Phase != GrenadePhase.None;
        /// <summary>Cook progress 0..1 (HUD fuse ticks, IWeaponHudState.CookTime01).</summary>
        public float Cook01 => _cook.Cook01;
        /// <summary>Fuse seconds left on the grenade in hand.</summary>
        public float FuseLeft => _cook.Remaining;
        /// <summary>The grenade is live in the hand (pin pulled, not thrown).</summary>
        public bool LiveInHand => _cook.Live;

        /// <summary>Pulls the pin. The caller has already taken a frag from the loadout.</summary>
        public void Begin()
        {
            Phase = GrenadePhase.PinPull;
            PhaseTime = 0f;
            _cook.Start(_spec.Fuse); // GDD §13.2: 4.0 s fuse from the pin pull
            _releaseQueued = false;
            _lastTick = 0;
        }

        /// <summary>Advances; <paramref name="held"/> is G held, <paramref name="released"/> is G released this frame.</summary>
        public Event Tick(float dt, bool held, bool released)
        {
            if (Phase == GrenadePhase.None) return Event.None;
            PhaseTime += dt;
            var ev = Event.None;
            if (_cook.Live)
            {
                if (_cook.Tick(dt))
                {
                    Phase = GrenadePhase.Recover;
                    PhaseTime = 0f;
                    return Event.ExplodedInHand;
                }
                if (_cook.WholeSeconds > _lastTick)
                {
                    _lastTick = _cook.WholeSeconds;
                    ev = Event.Tick;
                }
            }
            if (released || !held) _releaseQueued = true;
            switch (Phase)
            {
                case GrenadePhase.PinPull:
                    if (PhaseTime >= _spec.PinPullTime)
                    {
                        Phase = _releaseQueued ? GrenadePhase.Release : GrenadePhase.Cooking;
                        PhaseTime = 0f;
                    }
                    break;
                case GrenadePhase.Cooking:
                    if (_releaseQueued)
                    {
                        Phase = GrenadePhase.Release;
                        PhaseTime = 0f;
                    }
                    break;
                case GrenadePhase.Release:
                    if (PhaseTime >= _spec.ReleaseTime)
                    {
                        _thrownFuse = _cook.Release();
                        Phase = GrenadePhase.Recover;
                        PhaseTime = 0f;
                        return Event.Throw;
                    }
                    break;
                case GrenadePhase.Recover:
                    if (PhaseTime >= _spec.RecoverTime)
                    {
                        Phase = GrenadePhase.None;
                        PhaseTime = 0f;
                        return Event.Finished;
                    }
                    break;
            }
            return ev;
        }

        /// <summary>Fuse left on the grenade that was just thrown.</summary>
        public float ThrownFuse => _thrownFuse;

        /// <summary>Throw velocity along a view rotation (plus a share of the player's own velocity).</summary>
        public Vector3 ThrowVelocity(Quaternion view, Vector3 playerVelocity) =>
            GrenadeBallistics.ThrowVelocity(view, ThrowSpeed, _spec.ThrowPitchBias) + playerVelocity * 0.6f;

        /// <summary>Drops everything (checkpoint, death, mount). A live grenade in the hand is discarded.</summary>
        public void Cancel()
        {
            Phase = GrenadePhase.None;
            PhaseTime = 0f;
            _cook = default;
            _releaseQueued = false;
        }
    }
}
