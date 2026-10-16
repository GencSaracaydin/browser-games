using LongShadow.Combat.Data;
using UnityEngine;

namespace LongShadow.Combat.Ballistics
{
    /// <summary>
    /// Door-gun heat (GDD §13.3): +1 per shot, overheats at 55 (about 6 s of fire at 550 RPM), recovers 2.0 s after an
    /// overheat, or cools from full to zero in 1.0 s when idle. Idle cooling starts <see cref="DoorGunSpec.IdleDelay"/> after
    /// the last shot (otherwise sustained fire could never overheat). Mutable struct: keep it in a field.
    /// </summary>
    public struct HeatModel
    {
        /// <summary>Current heat (0 .. OverheatAt).</summary>
        public float Heat;
        /// <summary>True while overheated (firing blocked).</summary>
        public bool Overheated;
        /// <summary>Seconds since the overheat started.</summary>
        public float OverheatElapsed;
        /// <summary>Seconds since the last shot.</summary>
        public float SinceShot;

        /// <summary>True when the gun may fire.</summary>
        public bool CanFire => !Overheated;

        /// <summary>Heat 0..1 for the HUD bar.</summary>
        public float Heat01(DoorGunSpec s) => s == null || s.OverheatAt <= 0f ? 0f : Mathf.Clamp01(Heat / s.OverheatAt);

        /// <summary>Adds the heat of one shot; returns true when this shot overheated the gun.</summary>
        public bool OnShot(DoorGunSpec s)
        {
            if (Overheated) return false;
            Heat += s.HeatPerShot;
            SinceShot = 0f;
            if (Heat >= s.OverheatAt - 1e-4f)
            {
                Heat = s.OverheatAt;
                Overheated = true;
                OverheatElapsed = 0f;
                return true;
            }
            return false;
        }

        /// <summary>Advances cooling. <paramref name="firing"/> is true while the trigger is held and the gun is cycling.</summary>
        public void Tick(DoorGunSpec s, float dt, bool firing)
        {
            if (dt <= 0f) return;
            if (Overheated)
            {
                OverheatElapsed += dt;
                // Linear cool-down over the overheat recovery, then usable again from zero.
                Heat = s.OverheatRecovery > 0f ? Mathf.Max(0f, s.OverheatAt * (1f - OverheatElapsed / s.OverheatRecovery)) : 0f;
                if (OverheatElapsed >= s.OverheatRecovery - 1e-4f)
                {
                    Overheated = false;
                    Heat = 0f;
                    SinceShot = s.IdleDelay;
                }
                return;
            }
            SinceShot += dt;
            if (firing || SinceShot < s.IdleDelay) return;
            var rate = s.IdleRecovery > 0f ? s.OverheatAt / s.IdleRecovery : float.MaxValue;
            var cooling = Mathf.Min(dt, SinceShot - s.IdleDelay);
            Heat = Mathf.Max(0f, Heat - rate * cooling);
        }

        /// <summary>Cold gun.</summary>
        public void Reset()
        {
            Heat = 0f;
            Overheated = false;
            OverheatElapsed = 0f;
            SinceShot = 0f;
        }
    }
}
