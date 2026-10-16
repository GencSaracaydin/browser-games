using UnityEngine;

namespace LongShadow.Player.View
{
    /// <summary>
    /// Procedural head bob (GDD §12: 1.2 cm vertical and 0.8 cm lateral at walking pace, scaling with speed) and the
    /// footfall clock the footsteps and the view-model bob sync to (<c>IPlayerRig.BobPhase</c>). One step = π of phase;
    /// a footfall happens when the phase crosses a multiple of π (head lowest, vertical = −amplitude). Lateral sway
    /// follows the planted foot at half the step rate. Step cadence 1.4 + 0.3 × speed Hz (crouch 2.4 m/s ≈ 2.1 Hz,
    /// 1.1 m strides; walk 4.4 m/s ≈ 2.7 Hz, 1.6 m; sprint 6.9 m/s ≈ 3.5 Hz, 2.0 m: measured human jog/run cadences,
    /// DECISIONS WP02). Sprint adds a subtle roll (CINEMATIC_DIRECTION §4). Allocation-free.
    /// </summary>
    public sealed class HeadBob
    {
        /// <summary>GDD §12: vertical amplitude at walking pace (m).</summary>
        public float VerticalAmplitude = 0.012f;
        /// <summary>GDD §12: lateral amplitude at walking pace (m).</summary>
        public float LateralAmplitude = 0.008f;
        /// <summary>Roll at full sprint (degrees).</summary>
        public float SprintRollDegrees = 0.6f;
        /// <summary>Largest amplitude scale (speed / walk) so sprint bob stays readable.</summary>
        public float MaxScale = 1.6f;
        /// <summary>Bob reduction while aiming (1 = no bob at full ADS).</summary>
        public float AdsReduction = 0.75f;
        /// <summary>Bob factor while crouched.</summary>
        public float CrouchFactor = 0.75f;

        private float _scale;
        private float _phase;

        /// <summary>Phase in radians, wrapped to [0, 4π).</summary>
        public float Phase => _phase;
        /// <summary>Bob amount 0..1 (speed / sprint speed while grounded, smoothed).</summary>
        public float Amount01 { get; private set; }
        /// <summary>Local camera offset (x lateral, y vertical) in metres.</summary>
        public Vector3 Offset { get; private set; }
        /// <summary>Roll in degrees.</summary>
        public float Roll { get; private set; }
        /// <summary>Footfalls that happened during the last <see cref="Step"/> (0..2).</summary>
        public int Footfalls { get; private set; }

        /// <summary>Step cadence in Hz for a planar speed (0 when nearly still).</summary>
        public static float StepFrequency(float speed) => speed < 0.25f ? 0f : 1.4f + 0.3f * speed;

        /// <summary>Resets to rest.</summary>
        public void Reset()
        {
            _scale = 0f;
            _phase = 0f;
            Amount01 = 0f;
            Offset = Vector3.zero;
            Roll = 0f;
            Footfalls = 0;
        }

        /// <summary>
        /// Advances the bob. <paramref name="walkSpeed"/>/<paramref name="sprintSpeed"/> are the reference speeds,
        /// <paramref name="adsBlend"/> 0..1 reduces the bob while aiming.
        /// </summary>
        public void Step(float dt, float planarSpeed, bool grounded, float walkSpeed, float sprintSpeed, float adsBlend, bool crouched)
        {
            Footfalls = 0;
            if (dt <= 0f) return;
            var speed = grounded ? Mathf.Max(0f, planarSpeed) : 0f;
            var targetScale = walkSpeed > 0f ? Mathf.Min(speed / walkSpeed, MaxScale) : 0f;
            targetScale *= Mathf.Lerp(1f, 1f - AdsReduction, Mathf.Clamp01(adsBlend));
            if (crouched) targetScale *= CrouchFactor;
            // Fast attack, slower release: the bob settles instead of snapping when stopping.
            var lambda = targetScale > _scale ? 10f : 6f;
            _scale = Mathf.Lerp(_scale, targetScale, 1f - Mathf.Exp(-lambda * dt));

            var f = StepFrequency(speed);
            if (f > 0f)
            {
                var before = _phase;
                _phase += Mathf.PI * f * dt;
                Footfalls = Mathf.FloorToInt(_phase / Mathf.PI) - Mathf.FloorToInt(before / Mathf.PI);
                if (_phase >= 4f * Mathf.PI) _phase -= 4f * Mathf.PI;
            }

            Amount01 = Mathf.Lerp(Amount01, sprintSpeed > 0f && grounded ? Mathf.Clamp01(planarSpeed / sprintSpeed) : 0f, 1f - Mathf.Exp(-8f * dt));
            var vertical = -VerticalAmplitude * _scale * Mathf.Cos(2f * _phase);
            var lateral = LateralAmplitude * _scale * Mathf.Sin(_phase);
            Offset = new Vector3(lateral, vertical, 0f);
            var sprintFraction = sprintSpeed > walkSpeed ? Mathf.Clamp01((speed - walkSpeed) / (sprintSpeed - walkSpeed)) : 0f;
            Roll = SprintRollDegrees * sprintFraction * Mathf.Min(_scale, 1f) * Mathf.Sin(_phase);
        }
    }
}
