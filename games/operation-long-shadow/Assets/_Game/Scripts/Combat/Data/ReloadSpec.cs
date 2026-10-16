using System;

namespace LongShadow.Combat.Data
{
    /// <summary>
    /// One reload (tactical or empty) as keyed phase times in seconds from the reload start (GDD §13.1 "Reload animation:
    /// procedural from keyed transforms (tilt, magazine out, magazine in, charge)"). The view model interpolates between
    /// these keys; ammunition is committed at <see cref="MagIn"/> (cancelling before it keeps the old magazine).
    /// Treat instances as immutable (shared by <see cref="WeaponTable"/>).
    /// </summary>
    [Serializable]
    public sealed class ReloadSpec
    {
        /// <summary>Total duration in seconds (GDD §13.1 reload row).</summary>
        public float Duration;
        /// <summary>End of the tilt-in (the weapon rolls to show the magazine well).</summary>
        public float TiltEnd;
        /// <summary>The old magazine leaves the well (dropped on an empty reload, stowed on a tactical one).</summary>
        public float MagOut;
        /// <summary>The fresh magazine is in the left hand and starts travelling to the well.</summary>
        public float MagGrab;
        /// <summary>The fresh magazine seats ("slap"): ammunition is committed here.</summary>
        public float MagIn;
        /// <summary>Start of the charge (bolt / charging handle / slide release); 0 when the reload has no charge.</summary>
        public float ChargeStart;
        /// <summary>End of the charge (the bolt flies home).</summary>
        public float ChargeEnd;

        /// <summary>True when the reload cycles the action (empty reloads).</summary>
        public bool HasCharge => ChargeEnd > ChargeStart && ChargeStart > 0f;

        /// <summary>Creates a reload spec; every time is in seconds from the reload start.</summary>
        public ReloadSpec(float duration, float tiltEnd, float magOut, float magGrab, float magIn, float chargeStart = 0f, float chargeEnd = 0f)
        {
            Duration = duration;
            TiltEnd = tiltEnd;
            MagOut = magOut;
            MagGrab = magGrab;
            MagIn = magIn;
            ChargeStart = chargeStart;
            ChargeEnd = chargeEnd;
        }

        /// <summary>
        /// True when every key is ordered and inside the duration
        /// (0 &lt; TiltEnd &lt;= MagOut &lt; MagGrab &lt; MagIn &lt; [ChargeStart &lt; ChargeEnd] &lt; Duration).
        /// </summary>
        public bool IsValid()
        {
            if (!(Duration > 0f && TiltEnd > 0f && TiltEnd <= MagOut && MagOut < MagGrab && MagGrab < MagIn && MagIn < Duration)) return false;
            if (ChargeStart <= 0f && ChargeEnd <= 0f) return true;
            return ChargeStart >= MagIn && ChargeStart < ChargeEnd && ChargeEnd < Duration;
        }

        /// <summary>A copy with every key scaled by <paramref name="factor"/> (dev tuning).</summary>
        public ReloadSpec Scaled(float factor) =>
            new ReloadSpec(Duration * factor, TiltEnd * factor, MagOut * factor, MagGrab * factor, MagIn * factor,
                           ChargeStart * factor, ChargeEnd * factor);
    }
}
