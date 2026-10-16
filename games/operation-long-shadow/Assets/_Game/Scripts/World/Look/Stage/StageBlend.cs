using LongShadow.Core;

namespace LongShadow.World.Look.Stage
{
    /// <summary>
    /// Cross-fade state of the four stage volumes at one <c>s</c> (GDD §9.2a: each ramps in over 0.2 of <c>s</c> either side
    /// of a boundary). <see cref="Lower"/> is fully weighted and <see cref="Upper"/> is weighted by <see cref="Fraction"/>;
    /// with the stage volumes ordered by priority (Golden lowest), URP's blend then gives an exact cross-fade
    /// <c>lerp(lower, upper, fraction)</c>. Outside a ramp <see cref="Upper"/> equals <see cref="Lower"/> and the fraction is 0.
    /// </summary>
    public readonly struct StageBlend
    {
        /// <summary>Stage weighted 1.</summary>
        public readonly StageId Lower;
        /// <summary>Stage weighted <see cref="Fraction"/> (equals <see cref="Lower"/> outside a ramp).</summary>
        public readonly StageId Upper;
        /// <summary>Weight of <see cref="Upper"/> 0..1.</summary>
        public readonly float Fraction;

        /// <summary>Creates a blend.</summary>
        public StageBlend(StageId lower, StageId upper, float fraction)
        {
            Lower = lower;
            Upper = upper;
            Fraction = fraction;
        }

        /// <summary>Normalised weight of a stage (sums to 1 over the four stages).</summary>
        public float WeightOf(StageId stage)
        {
            if (Lower == Upper) return stage == Lower ? 1f : 0f;
            if (stage == Lower) return 1f - Fraction;
            if (stage == Upper) return Fraction;
            return 0f;
        }

        /// <summary>Volume weight of a stage under the priority scheme (lower 1, upper fraction, others 0).</summary>
        public float VolumeWeightOf(StageId stage)
        {
            if (stage == Lower) return 1f;
            if (stage == Upper) return Fraction;
            return 0f;
        }

        /// <summary>Linear blend of per-stage values (Golden, Sunset, Afterglow, Blue Hour).</summary>
        public float Blend(float golden, float sunset, float afterglow, float blueHour)
        {
            var lo = Pick(Lower, golden, sunset, afterglow, blueHour);
            if (Lower == Upper) return lo;
            var hi = Pick(Upper, golden, sunset, afterglow, blueHour);
            return lo + (hi - lo) * Fraction;
        }

        private static float Pick(StageId s, float g, float su, float a, float b)
        {
            switch (s)
            {
                case StageId.Golden: return g;
                case StageId.Sunset: return su;
                case StageId.Afterglow: return a;
                default: return b;
            }
        }

        /// <inheritdoc />
        public override string ToString() => Lower == Upper ? Lower.ToString() : Lower + "→" + Upper + " " + Fraction.ToString("0.00");
    }
}
