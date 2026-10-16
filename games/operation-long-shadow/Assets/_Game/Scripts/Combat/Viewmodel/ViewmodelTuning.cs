using System;
using UnityEngine;

namespace LongShadow.Combat.Viewmodel
{
    /// <summary>
    /// Feel constants of the procedural view model (tuning, not GDD numbers; the GDD numbers are the 2.2 cm kick and the
    /// 30 % camera share in RecoilSpec, the 1.2/0.8 cm walk bob of GDD §12, and CINEMATIC_DIRECTION §4 "sway lagging the
    /// look by about 1.5°"). One shared instance lives on the ViewmodelController so an editor holder can tune it live.
    /// </summary>
    [Serializable]
    public sealed class ViewmodelTuning
    {
        [Header("Sway (mouse lag + inertia)")]
        [Tooltip("Degrees of lag per degree/second of look speed.")]
        public float SwayPerDegPerSec = 0.0085f;
        [Tooltip("Largest lag angle (degrees).")]
        public float SwayMaxDegrees = 4f;
        [Tooltip("Metres of lateral lag per degree/second of yaw speed.")]
        public float SwayPositionPerDegPerSec = 0.000055f;
        [Tooltip("Largest positional lag (metres).")]
        public float SwayMaxPosition = 0.018f;
        [Tooltip("Sway spring frequency (Hz); heavier weapons are slower.")]
        public float SwayFrequency = 6.5f;
        [Tooltip("Sway spring damping (<1 overshoots: inertia on turn).")]
        public float SwayDamping = 0.55f;
        [Tooltip("Sway kept while aiming.")]
        public float SwayAdsScale = 0.28f;

        [Header("Bob (synced to IPlayerRig.BobPhase; GDD §12 1.2 cm / 0.8 cm at walk)")]
        public Vector2 BobAmplitude = new Vector2(0.0075f, 0.0065f);
        [Tooltip("Bob roll (degrees) and yaw (degrees).")]
        public Vector2 BobRotation = new Vector2(0.9f, 0.55f);
        [Tooltip("BobAmount01 at walking speed (4.4 / 6.9 m/s).")]
        public float WalkBobAmount = 0.64f;
        public float BobAdsScale = 0.12f;
        [Tooltip("Extra bob multiplier at full sprint.")]
        public float SprintBobScale = 1.9f;

        [Header("Idle breathing")]
        public Vector2 BreathAmplitude = new Vector2(0.0008f, 0.0012f);
        public float BreathRotation = 0.14f;
        public Vector2 BreathFrequency = new Vector2(0.13f, 0.25f);
        public float BreathAdsScale = 0.55f;

        [Header("Kick (GDD §13.1: 2.2 cm back, spring return)")]
        public float KickPositionFrequency = 11f;
        public float KickPositionDamping = 0.62f;
        public float KickRotationFrequency = 9f;
        public float KickRotationDamping = 0.58f;
        [Tooltip("Rotational kick kept while aiming (the camera punch carries the rest).")]
        public float KickAdsRotationScale = 0.35f;
        [Tooltip("Positional kick kept while aiming.")]
        public float KickAdsPositionScale = 0.8f;
        [Tooltip("Upward share of the backward kick.")]
        public float KickRise = 0.18f;

        [Header("Movement")]
        [Tooltip("Roll per walking-speed of strafe (degrees).")]
        public float StrafeRollDegrees = 2.6f;
        [Tooltip("Pull-back while walking forward (metres).")]
        public float ForwardPull = 0.008f;
        [Tooltip("Lift while airborne (metres; the gun lags a jump).")]
        public float AirLift = 0.012f;
        [Tooltip("Landing dip per m/s of landing speed (metres) and its cap.")]
        public Vector2 LandingDip = new Vector2(0.006f, 0.05f);
        [Tooltip("Landing pitch per m/s (degrees) and its cap.")]
        public Vector2 LandingPitch = new Vector2(0.5f, 4f);
        [Tooltip("Pose spring for movement offsets.")]
        public float MoveFrequency = 5f;
        public float MoveDamping = 0.8f;

        [Header("Poses")]
        [Tooltip("Seconds to blend into the sprint pose.")]
        public float SprintInTime = 0.22f;
        [Tooltip("Seconds to blend into and out of crouch/slide poses.")]
        public float StancePoseTime = 0.18f;
        [Tooltip("View-model camera vertical FOV at the hip (TECH_PLAN §3.6: 55°).")]
        public float HipViewmodelFov = 55f;

        /// <summary>Sway spring frequency for a weapon's heft (0 light .. 1 heavy).</summary>
        public float SwayFrequencyFor(float heft) => SwayFrequency * Mathf.Lerp(1.1f, 0.65f, Mathf.Clamp01(heft));

        /// <summary>Kick spring frequency for a weapon's heft.</summary>
        public float KickFrequencyFor(float heft) => KickPositionFrequency * Mathf.Lerp(1.1f, 0.8f, Mathf.Clamp01(heft));
    }
}
