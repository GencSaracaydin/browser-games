using LongShadow.Combat.Data;

namespace LongShadow.Combat.Weapons
{
    /// <summary>
    /// Reload state machine (TECH_PLAN §7.5: "reload state machine (tactical vs empty, cancel on sprint/switch)"). An empty
    /// magazine selects the empty reload (with the charge); ammunition moves into the magazine at
    /// <see cref="ReloadSpec.MagIn"/>, so cancelling before the seat keeps the old magazine and cancelling after it keeps
    /// the new one (Black Ops rule: a reload cancelled after the magazine seats still counts).
    /// </summary>
    public sealed class ReloadStateMachine
    {
        /// <summary>True while reloading.</summary>
        public bool IsReloading { get; private set; }
        /// <summary>True for the empty reload.</summary>
        public bool IsEmptyReload { get; private set; }
        /// <summary>Seconds since the reload started.</summary>
        public float Elapsed { get; private set; }
        /// <summary>The running reload's keys.</summary>
        public ReloadSpec Spec { get; private set; }
        /// <summary>True once the new magazine seated (ammo committed).</summary>
        public bool Committed { get; private set; }

        /// <summary>Reload progress 0..1.</summary>
        public float Normalized => Spec != null && Spec.Duration > 0f ? Elapsed / Spec.Duration : 0f;

        /// <summary>Starts a reload when it would add rounds; returns <see cref="ReloadEvents.Started"/> or None.</summary>
        public ReloadEvents Begin(WeaponSpec w, WeaponAmmo ammo)
        {
            if (IsReloading || w == null || ammo == null || !ammo.CanReload) return ReloadEvents.None;
            IsEmptyReload = ammo.IsEmpty;
            Spec = IsEmptyReload ? w.EmptyReload : w.TacticalReload;
            if (Spec == null) return ReloadEvents.None;
            Elapsed = 0f;
            Committed = false;
            IsReloading = true;
            return ReloadEvents.Started;
        }

        /// <summary>Advances the reload; commits ammo at the magazine seat; returns the keys crossed.</summary>
        public ReloadEvents Tick(float dt, WeaponAmmo ammo)
        {
            if (!IsReloading || Spec == null) return ReloadEvents.None;
            var prev = Elapsed;
            Elapsed += dt;
            var e = ReloadEvents.None;
            if (Crossed(prev, Elapsed, Spec.TiltEnd)) e |= ReloadEvents.TiltDone;
            if (Crossed(prev, Elapsed, Spec.MagOut)) e |= ReloadEvents.MagOut;
            if (Crossed(prev, Elapsed, Spec.MagGrab)) e |= ReloadEvents.MagGrab;
            if (Crossed(prev, Elapsed, Spec.MagIn))
            {
                e |= ReloadEvents.MagIn;
                ammo?.CommitReload();
                Committed = true;
            }
            if (Spec.HasCharge)
            {
                if (Crossed(prev, Elapsed, Spec.ChargeStart)) e |= ReloadEvents.ChargeStart;
                if (Crossed(prev, Elapsed, Spec.ChargeEnd)) e |= ReloadEvents.ChargeEnd;
            }
            if (Elapsed >= Spec.Duration)
            {
                if (!Committed)
                {
                    ammo?.CommitReload();
                    Committed = true;
                }
                e |= ReloadEvents.Finished;
                IsReloading = false;
            }
            return e;
        }

        /// <summary>Cancels a running reload (ammo already committed stays).</summary>
        public ReloadEvents Cancel()
        {
            if (!IsReloading) return ReloadEvents.None;
            IsReloading = false;
            return ReloadEvents.Cancelled;
        }

        /// <summary>Forgets everything (weapon swap, checkpoint).</summary>
        public void Reset()
        {
            IsReloading = false;
            IsEmptyReload = false;
            Elapsed = 0f;
            Committed = false;
            Spec = null;
        }

        private static bool Crossed(float prev, float now, float key) => key > 0f && prev < key && now >= key;
    }
}
