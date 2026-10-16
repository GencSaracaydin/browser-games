using System;
using UnityEngine;

namespace LongShadow.Player.View
{
    /// <summary>
    /// Tunables of <see cref="PlayerCameraRig"/>. Camera values are TECH_PLAN §3.6; FOV kicks are CINEMATIC_DIRECTION §4
    /// (sprint +5°, which overrides TECH_PLAN's +6°) and TECH_PLAN §7.3 (slide +4°); the rest is feel (logged in
    /// Docs/DECISIONS.md).
    /// </summary>
    [Serializable]
    public sealed class CameraSettings
    {
        [Header("Cameras (TECH_PLAN §3.6)")]
        public float WorldNear = 0.05f;
        public float WorldFar = 3000f;
        [Tooltip("TECH_PLAN D5: view-model FOV 55° vertical (weapons may change it for ADS).")]
        public float ViewmodelFov = 55f;
        public float ViewmodelNear = 0.01f;
        public float ViewmodelFar = 5f;
        [Tooltip("Where post-processing runs in the stack (see ViewmodelPostMode).")]
        public ViewmodelPostMode PostMode = ViewmodelPostMode.GradeOnceOnOverlay;

        [Header("FOV kicks, horizontal degrees")]
        [Tooltip("CINEMATIC_DIRECTION §4: FOV kick on sprint +5°.")] public float SprintKick = 5f;
        [Tooltip("TECH_PLAN §7.3: slide +4°.")] public float SlideKick = 4f;
        public float KickRiseLambda = 7f;
        public float KickFallLambda = 4f;

        [Header("Eye")]
        [Tooltip("Eye position relative to the neck pivot (looking down moves the eye forward, like a head).")]
        public Vector3 EyeOffsetFromNeck = new Vector3(0f, 0.1f, 0.06f);
        [Tooltip("Crouch/stand/slide eye transition spring (Hz, critically damped).")]
        public float EyeSpringFrequency = 4.5f;

        [Header("Landing dip (scaled by impact speed)")]
        public float LandDipPerMps = 0.009f;
        public float MaxLandDip = 0.14f;
        public float LandNodPerMps = 0.35f;
        public float MaxLandNod = 4f;
        public float LandSpringFrequency = 3.2f;
        public float LandSpringDamping = 0.5f;
        [Tooltip("Impacts below this speed (m/s) do not dip the camera.")]
        public float LandMinSpeed = 2.5f;

        [Header("Trauma shake (IPlayerRig.Shake)")]
        public Vector3 ShakeMaxDegrees = new Vector3(4f, 4f, 2.5f);
        public Vector3 ShakeMaxOffset = new Vector3(0.02f, 0.02f, 0.01f);
        public float ShakeFrequency = 18f;
        public float TraumaDecayPerSecond = 1.3f;

        [Header("View punch (IPlayerRig.SetViewPunch, damage)")]
        public float PunchFrequency = 6f;
        public float PunchDamping = 0.55f;
        public float DamagePunchDegrees = 2.2f;
        public float DamageTraumaPerHp = 0.004f;

        [Header("Tilt and sway")]
        [Tooltip("CINEMATIC_DIRECTION §4: gentle tilt on strafe (degrees at walk speed).")]
        public float StrafeRollDegrees = 0.9f;
        [Tooltip("CINEMATIC_DIRECTION §4: breathing sway while aiming (degrees).")]
        public float AdsBreathDegrees = 0.12f;
        [Tooltip("Hand-held sway while glassing with binoculars (degrees).")]
        public float GlassingSwayDegrees = 0.2f;

        [Header("Traversal")]
        public float VaultDip = 0.07f;
        public float VaultRollDegrees = 3f;
        public float ClimbNodDegrees = 7f;
        public float MantleNodDegrees = 5f;

        [Header("Death")]
        public float DeathEyeHeight = 0.3f;
        public float DeathRollDegrees = 38f;
        public float DeathPitchDegrees = 12f;
        public float DeathSeconds = 0.9f;
    }
}
