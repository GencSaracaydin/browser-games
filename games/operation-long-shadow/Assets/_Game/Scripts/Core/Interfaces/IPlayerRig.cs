using System;
using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// The player rig (WP02 PlayerRig). Weapons, mission scripts, cinematics and the HUD read and drive the player only
    /// through this interface; the player never references weapons.
    /// </summary>
    public interface IPlayerRig
    {
        /// <summary>Rig root (feet, yaw).</summary>
        Transform Root { get; }
        /// <summary>Eye transform (camera position, pitch applied).</summary>
        Transform Eye { get; }
        /// <summary>World camera (Cam_World, base camera).</summary>
        Camera WorldCamera { get; }
        /// <summary>View-model overlay camera (Cam_Viewmodel).</summary>
        Camera ViewmodelCamera { get; }
        /// <summary>Parent for the first-person weapon and body (layer Viewmodel), under the camera rig.</summary>
        Transform ViewmodelRoot { get; }
        /// <summary>World velocity in m/s.</summary>
        Vector3 Velocity { get; }
        /// <summary>Current locomotion state.</summary>
        Stance Stance { get; }
        /// <summary>True when standing on ground.</summary>
        bool IsGrounded { get; }
        /// <summary>Aiming down sights (set by the weapon through <see cref="SetAdsState"/>).</summary>
        bool IsADS { get; }
        /// <summary>ADS blend 0..1.</summary>
        float AdsBlend { get; }
        /// <summary>Weapon move-speed multiplier (GDD §13.1 "move multiplier"); 1 = none.</summary>
        float SpeedMultiplier { set; }
        /// <summary>Seconds since sprint ended (sprint-out-to-fire 0.20 s / 0.32 s RPK).</summary>
        float TimeSinceSprintEnd { get; }
        /// <summary>Head-bob phase in radians (view-model bob syncs to it).</summary>
        float BobPhase { get; }
        /// <summary>Head-bob amount 0..1 (scales with speed).</summary>
        float BobAmount01 { get; }
        /// <summary>Ray from the eye along the view.</summary>
        Ray ViewRay { get; }
        /// <summary>Binoculars raised (Beat 1 glassing).</summary>
        bool IsGlassing { get; }
        /// <summary>View pitch in degrees (positive = up, clamped to +-85).</summary>
        float Pitch { get; }
        /// <summary>View yaw in degrees (compass heading: 0 = +Z, 90 = +X).</summary>
        float Yaw { get; }
        /// <summary>Raised on landing with the downward speed in m/s (view-model and camera dips).</summary>
        event Action<float> Landed;

        /// <summary>Recoil that moves the aim (degrees).</summary>
        void AddAimDelta(float pitch, float yaw);
        /// <summary>Visual-only view punch (returns by spring).</summary>
        void SetViewPunch(Quaternion punch);
        /// <summary>Weapon reports its ADS state each frame.</summary>
        void SetAdsState(bool isAds, float blend01);
        /// <summary>Requests a vertical FOV (degrees) blended over <paramref name="blendTime"/>; the latest owner wins.</summary>
        void RequestFov(object owner, float vFov, float blendTime);
        /// <summary>Releases an FOV request.</summary>
        void ReleaseFov(object owner);
        /// <summary>Adds camera trauma 0..1 (shake).</summary>
        void Shake(float trauma);
        /// <summary>Moves the player instantly (checkpoints, mounts).</summary>
        void Teleport(Vector3 position, float yaw);
        /// <summary>Sets the view angles instantly (cinematic hand-offs, forced head turns tweened by WP09).</summary>
        void SetLookAngles(float pitch, float yaw);
        /// <summary>
        /// Enables or disables the rig's cameras (Cam_World and its overlay) while a cinematic or the booth owns the
        /// screen. Motor, health and input are not affected (lock input separately with <see cref="IInputLock"/>).
        /// </summary>
        void SetViewActive(bool active);
        /// <summary>Forces walking at a speed (MID-A 1.2 m/s); null releases.</summary>
        void SetForcedWalk(float? speed);
        /// <summary>Limits look around a centre orientation (door gun, cinematics).</summary>
        void SetLookLimits(Quaternion center, float yaw, float up, float down);
        /// <summary>Removes look limits.</summary>
        void ClearLookLimits();
        /// <summary>Damage scale and HP floor (Beat 11: 0.25, floor 1).</summary>
        void SetDamageModifiers(float scale, float floorHp);
        /// <summary>Parents the player to a seat and disables the motor.</summary>
        void Mount(Transform seat);
        /// <summary>Leaves the current seat.</summary>
        void Dismount();
        /// <summary>Makes the binoculars available (Beat 1 only).</summary>
        void SetBinocularsAvailable(bool available);
        /// <summary>Shows/hides the first-person body (view model) and the shadow proxy (TECH_PLAN D19).</summary>
        void SetBodyVisibility(bool viewmodel, bool shadowProxy);
        /// <summary>
        /// Scripted posture: overrides the eye height above the feet (Beat 1 start prone about 0.35 m, then the scripted
        /// kneel at 1.05 m, GDD §7.1/§8.3), blended over <paramref name="blendSeconds"/>; null returns to the stance height.
        /// The capsule is unchanged; the shadow proxy hides while the eye is below 0.7 m (AC_ShadowProxy has no prone state).
        /// </summary>
        void SetEyeHeightOverride(float? eyeHeight, float blendSeconds);
    }
}
