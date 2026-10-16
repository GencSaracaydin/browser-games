using LongShadow.Core;
using UnityEngine;

namespace LongShadow.AI.Common
{
    /// <summary>
    /// Perception rules of GDD §14.2 as pure functions: sight range by LightStage (70 / 60 / 45 / 35 m), doubled up to a
    /// 70 m cap inside lit zones, field of view 120° (160° within 8 m), detection 0.4-1.2 s by distance (crouch ×1.5
    /// slower, sprint ×0.6 faster) and the hearing radii.
    /// </summary>
    public static class PerceptionMath
    {
        /// <summary>Sight range at GOLDEN (GDD §14.2); also the fallback when no ILightStage is registered.</summary>
        public const float GoldenRange = 70f;
        /// <summary>Sight range at SUNSET.</summary>
        public const float SunsetRange = 60f;
        /// <summary>Sight range at AFTERGLOW.</summary>
        public const float AfterglowRange = 45f;
        /// <summary>Sight range at BLUE HOUR.</summary>
        public const float BlueHourRange = 35f;
        /// <summary>Lit-zone multiplier (flood cone, flare, headlights).</summary>
        public const float LitMultiplier = 2f;
        /// <summary>Lit-zone cap.</summary>
        public const float LitCap = 70f;
        /// <summary>Normal field of view in degrees.</summary>
        public const float Fov = 120f;
        /// <summary>Close field of view in degrees (within <see cref="CloseFovDistance"/>).</summary>
        public const float CloseFov = 160f;
        /// <summary>Distance at which the field of view widens.</summary>
        public const float CloseFovDistance = 8f;
        /// <summary>Fastest detection (close).</summary>
        public const float MinDetectionSeconds = 0.4f;
        /// <summary>Slowest detection (at the edge of sight range).</summary>
        public const float MaxDetectionSeconds = 1.2f;
        /// <summary>Distance at or below which detection is fastest.</summary>
        public const float FastDetectionDistance = 5f;
        /// <summary>Crouching target: detection ×1.5 slower.</summary>
        public const float CrouchDetectionFactor = 1.5f;
        /// <summary>Sprinting target: detection ×0.6 (faster).</summary>
        public const float SprintDetectionFactor = 0.6f;

        // Hearing radii (GDD §14.2). WP02/WP03 raise Noise/ShotFired with these radii; the AI scales them by
        // ILightStage.HearingScale.
        /// <summary>Unsuppressed shot.</summary>
        public const float HearShot = 60f;
        /// <summary>Hush Puppy.</summary>
        public const float HearSuppressedShot = 6f;
        /// <summary>Frag explosion.</summary>
        public const float HearFrag = 80f;
        /// <summary>Squad gunfire.</summary>
        public const float HearSquadGunfire = 40f;
        /// <summary>Sprint footsteps.</summary>
        public const float HearSprint = 12f;
        /// <summary>Walk footsteps.</summary>
        public const float HearWalk = 4f;
        /// <summary>Crouch footsteps.</summary>
        public const float HearCrouch = 1.5f;

        /// <summary>Sight range of a named stage (GDD §14.2).</summary>
        public static float StageRange(StageId stage)
        {
            switch (stage)
            {
                case StageId.Sunset: return SunsetRange;
                case StageId.Afterglow: return AfterglowRange;
                case StageId.BlueHour: return BlueHourRange;
                default: return GoldenRange;
            }
        }

        /// <summary>
        /// Effective sight range: the stage range, doubled up to <paramref name="litCap"/> when the target is lit, or a
        /// fixed range for scoped shooters (marksman 160 m, &gt; 0 overrides everything).
        /// </summary>
        public static float EffectiveRange(float stageRange, bool targetLit, float fixedRange = 0f,
                                           float litMultiplier = LitMultiplier, float litCap = LitCap)
        {
            if (fixedRange > 0f) return fixedRange;
            if (!targetLit) return stageRange;
            return Mathf.Max(stageRange, Mathf.Min(stageRange * litMultiplier, litCap));
        }

        /// <summary>Field of view in degrees at a distance (120°, 160° within 8 m).</summary>
        public static float FovAt(float distance) => distance <= CloseFovDistance ? CloseFov : Fov;

        /// <summary>True if <paramref name="toTarget"/> is inside the field of view around <paramref name="forward"/> (planar).</summary>
        public static bool InFov(Vector3 forward, Vector3 toTarget, float distance)
        {
            forward.y = 0f;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 1e-4f) return true;
            if (forward.sqrMagnitude < 1e-6f) return true;
            return Vector3.Angle(forward, toTarget) <= FovAt(distance) * 0.5f;
        }

        /// <summary>True if <paramref name="toTarget"/> is inside a fixed field of view of <paramref name="fovDegrees"/> (planar).</summary>
        public static bool InFov(Vector3 forward, Vector3 toTarget, float distance, float fovDegrees)
        {
            forward.y = 0f;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 1e-4f) return true;
            if (forward.sqrMagnitude < 1e-6f) return true;
            return Vector3.Angle(forward, toTarget) <= fovDegrees * 0.5f;
        }

        /// <summary>Seconds to detect a target at a distance (GDD §14.2), with its stance factor.</summary>
        public static float DetectionSeconds(float distance, float sightRange, Stance targetStance)
        {
            var far = Mathf.Max(FastDetectionDistance + 1f, sightRange);
            var t = Mathf.InverseLerp(FastDetectionDistance, far, distance);
            var s = Mathf.Lerp(MinDetectionSeconds, MaxDetectionSeconds, t);
            if (targetStance == Stance.Crouch || targetStance == Stance.Slide) s *= CrouchDetectionFactor;
            else if (targetStance == Stance.Sprint) s *= SprintDetectionFactor;
            return s;
        }

        /// <summary>True if a listener at <paramref name="listener"/> hears the noise (radius scaled by HearingScale).</summary>
        public static bool CanHear(in NoiseEvent e, Vector3 listener, float hearingScale)
        {
            var r = e.Radius * (hearingScale > 0f ? hearingScale : 1f);
            return r > 0f && (e.Position - listener).sqrMagnitude <= r * r;
        }

        /// <summary>True for noises that put an enemy straight into combat (shots, explosions, impacts); footsteps and voices only raise suspicion.</summary>
        public static bool IsAlarming(NoiseKind kind) => kind != NoiseKind.Footstep && kind != NoiseKind.Voice;
    }
}
