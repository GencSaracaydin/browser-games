using System;
using UnityEngine;

namespace LongShadow.Player.Movement
{
    /// <summary>
    /// Tunables of <see cref="PlayerMotor"/>. Every number that comes from the design cites its section (R9); the
    /// rest are feel values chosen for the CoD-arcade target (tight, responsive, weighty) and logged in
    /// Docs/DECISIONS.md. Serialised on the motor so the editor can tweak them, but the defaults are the spec.
    /// </summary>
    [Serializable]
    public sealed class MotorSettings
    {
        [Header("Speeds (GDD §12)")]
        [Tooltip("GDD §12: walk 4.4 m/s.")] public float WalkSpeed = 4.4f;
        [Tooltip("GDD §12: ADS walk 2.6 m/s.")] public float AdsWalkSpeed = 2.6f;
        [Tooltip("GDD §12: crouch 2.4 m/s.")] public float CrouchSpeed = 2.4f;
        [Tooltip("GDD §12: sprint 6.9 m/s, unlimited.")] public float SprintSpeed = 6.9f;
        [Tooltip("Speed factor when moving straight backwards (feel; DECISIONS WP02).")]
        public float BackpedalFactor = 0.8f;

        [Header("Acceleration (feel)")]
        [Tooltip("m/s² from standstill up to walk speed (0 → 4.4 m/s in ~0.15 s).")]
        public float GroundAcceleration = 30f;
        [Tooltip("m/s² when slowing down or reversing.")]
        public float GroundDeceleration = 24f;
        [Tooltip("m/s² above walk speed (walk → sprint in ~0.25 s: weight).")]
        public float SprintAcceleration = 10f;
        [Tooltip("m/s² of air control (momentum is kept; steering is limited).")]
        public float AirAcceleration = 4f;
        [Tooltip("m/s² (CoD-like 20 m/s²: snappy, weighty jumps; DECISIONS WP02).")]
        public float Gravity = 20f;
        [Tooltip("Maximum fall speed in m/s.")]
        public float TerminalVelocity = 55f;
        [Tooltip("Downward speed while grounded so the controller keeps contact on slopes and steps.")]
        public float GroundStickSpeed = 2f;
        [Tooltip("Ground snapping distance when walking off small steps and down slopes.")]
        public float GroundSnapDistance = 0.45f;

        [Header("Capsule (GDD §12)")]
        [Tooltip("GDD §12: capsule 1.8 m.")] public float StandHeight = 1.8f;
        [Tooltip("TECH_PLAN §7.3: crouch capsule 1.1 m.")] public float CrouchHeight = 1.1f;
        public float Radius = 0.3f;
        [Tooltip("TECH_PLAN §7.3: step 0.35 m.")] public float StepOffset = 0.35f;
        [Tooltip("TECH_PLAN §7.3: slope 45°.")] public float SlopeLimit = 45f;
        public float SkinWidth = 0.03f;
        [Tooltip("GDD §12: eye height 1.65 m standing.")] public float StandEyeHeight = 1.65f;
        [Tooltip("GDD §12: eye height 1.05 m crouched.")] public float CrouchEyeHeight = 1.05f;

        [Header("Jump (GDD §12)")]
        [Tooltip("GDD §12: jump apex 1.0 m.")] public float JumpApexHeight = 1.0f;
        [Tooltip("Seconds after leaving a ledge during which a jump still counts.")]
        public float CoyoteTime = 0.12f;
        [Tooltip("Seconds a jump press is remembered before landing.")]
        public float JumpBufferTime = 0.12f;

        [Header("Slide (GDD §12)")]
        [Tooltip("GDD §12: entered at >= 5.5 m/s with C.")] public float SlideEntrySpeed = 5.5f;
        [Tooltip("GDD §12: 8.0 m/s at the start.")] public float SlideStartSpeed = 8f;
        [Tooltip("GDD §12: 4.0 m/s at the end.")] public float SlideEndSpeed = 4f;
        [Tooltip("GDD §12: lasts 0.75 s.")] public float SlideDuration = 0.75f;
        [Tooltip("GDD §12: 0.6 m camera dip.")] public float SlideCameraDip = 0.6f;
        [Tooltip("Steering while sliding, degrees per second.")]
        public float SlideSteerDegreesPerSecond = 40f;
        [Tooltip("Seconds before another slide can start.")]
        public float SlideCooldown = 0.35f;

        [Header("Vault / climb / air mantle (GDD §12)")]
        [Tooltip("GDD §12: ledges 0.6–1.4 m auto-vault.")] public float VaultMinHeight = 0.6f;
        public float VaultMaxHeight = 1.4f;
        [Tooltip("GDD §12: vault in 0.45 s.")] public float VaultDuration = 0.45f;
        [Tooltip("GDD §12: ledges 1.4–2.1 m are climbed on Space.")] public float ClimbMaxHeight = 2.1f;
        [Tooltip("GDD §12: climb in 0.8 s.")] public float ClimbDuration = 0.8f;
        [Tooltip("GDD §12: air mantle when the ledge top is 0–1.4 m above the feet.")]
        public float AirMantleMaxAboveFeet = 1.4f;
        [Tooltip("Lowest ledge top above the feet that the air mantle grabs (below that the player simply lands).")]
        public float AirMantleMinAboveFeet = 0.1f;
        [Tooltip("GDD §12: ...and <= 0.8 m ahead (face distance from the body centre).")]
        public float AirMantleReach = 0.8f;
        [Tooltip("GDD §12: air mantle climb in 0.5 s.")] public float AirMantleDuration = 0.5f;
        [Tooltip("Ground vault/climb: face within this distance from the body centre.")]
        public float GroundLedgeReach = 0.75f;
        [Tooltip("Thickest wall that is vaulted over instead of onto (metres).")]
        public float OverVaultMaxThickness = 0.7f;

        [Header("Fall damage (GDD §12)")]
        [Tooltip("GDD §12: none below 4.5 m.")] public float SafeFallHeight = 4.5f;
        [Tooltip("GDD §12: 30 HP per metre above that.")] public float FallDamagePerMetre = 30f;

        [Header("Ladders (TECH_PLAN §7.3)")]
        [Tooltip("Seconds of the step-off at the top of a ladder.")]
        public float LadderExitDuration = 0.4f;
        [Tooltip("Vertical metres per rung sound.")]
        public float LadderRungSpacing = 0.33f;
        [Tooltip("Looking further down than this (degrees) makes W climb down.")]
        public float LadderLookDownPitch = -45f;
    }
}
