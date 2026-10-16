using LongShadow.Combat.Data;
using UnityEngine;

namespace LongShadow.Combat.Ballistics
{
    /// <summary>
    /// Spread state of the held weapon: GDD §13.1 hip/ADS cones blended by the ADS blend, widened by movement, air and
    /// per-shot bloom, narrowed slightly when crouched (TECH_PLAN §7.5 "spread model"). Mutable struct: keep it in a field.
    /// </summary>
    public struct SpreadModel
    {
        /// <summary>Current hip bloom in degrees.</summary>
        public float Bloom;

        /// <summary>Spread cone half-angle in degrees for the current state.</summary>
        /// <param name="s">The weapon's spread spec.</param>
        /// <param name="adsBlend">0 hip .. 1 fully aimed.</param>
        /// <param name="move01">Horizontal speed relative to walking speed (0 still, 1 walk, &gt;1 sprint/slide).</param>
        /// <param name="grounded">False while airborne.</param>
        /// <param name="crouched">True while crouched.</param>
        public float Current(SpreadSpec s, float adsBlend, float move01, bool grounded, bool crouched)
        {
            if (s == null) return 0f;
            var m = Mathf.Clamp(move01, 0f, 1.5f);
            var hip = s.Hip + s.MoveAdd * m + Bloom;
            var ads = s.Ads + s.AdsMoveAdd * Mathf.Min(m, 1f);
            var spread = Mathf.Lerp(hip, ads, Mathf.Clamp01(adsBlend));
            if (!grounded) spread *= s.AirborneMul;
            else if (crouched && m < 0.2f) spread *= s.CrouchMul;
            return Mathf.Max(0f, spread);
        }

        /// <summary>Adds per-shot bloom.</summary>
        public void OnShot(SpreadSpec s)
        {
            if (s == null) return;
            Bloom = Mathf.Min(s.BloomMax, Bloom + s.BloomPerShot);
        }

        /// <summary>Decays bloom.</summary>
        public void Tick(SpreadSpec s, float dt)
        {
            if (s == null) return;
            Bloom = Mathf.Max(0f, Bloom - s.BloomDecay * dt);
        }

        /// <summary>Resets bloom (weapon switch).</summary>
        public void Reset() => Bloom = 0f;

        /// <summary>
        /// Deviates <paramref name="aim"/>'s forward inside a cone of <paramref name="spreadDegrees"/> half-angle, uniformly
        /// over the cone's disc. <paramref name="u1"/>, <paramref name="u2"/> are uniform randoms in [0, 1).
        /// </summary>
        public static Vector3 Deviate(Quaternion aim, float spreadDegrees, float u1, float u2)
        {
            if (spreadDegrees <= 0f) return aim * Vector3.forward;
            var radius = spreadDegrees * Mathf.Sqrt(Mathf.Clamp01(u1));
            var theta = u2 * 2f * Mathf.PI;
            var pitch = radius * Mathf.Sin(theta);
            var yaw = radius * Mathf.Cos(theta);
            return aim * (Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward);
        }

        /// <summary>Normalised crosshair opening for the HUD.</summary>
        public static float Crosshair01(SpreadSpec s, float spreadDegrees) =>
            s == null || s.CrosshairMax <= 0f ? 0f : Mathf.Clamp01(spreadDegrees / s.CrosshairMax);
    }
}
