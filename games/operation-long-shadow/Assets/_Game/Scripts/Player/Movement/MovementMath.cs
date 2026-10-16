using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Player.Movement
{
    /// <summary>
    /// Pure movement math of the player (GDD §12, TECH_PLAN §7.3): target speeds, acceleration, jump, slide curve,
    /// ledge classification, fall damage and capsule/eye heights. No Unity scene access, so EditMode tests cover it.
    /// </summary>
    public static class MovementMath
    {
        /// <summary>
        /// Target ground speed in m/s for a stance (GDD §12): walk 4.4 blending to ADS walk 2.6 by <paramref name="adsBlend"/>,
        /// crouch 2.4, sprint 6.9. Multiplied by the weapon move multiplier (GDD §13.1) and capped by a forced walk
        /// (MID-A 1.2 m/s) when <paramref name="forcedWalk"/> &gt; 0.
        /// </summary>
        public static float GroundSpeed(MotorSettings s, Stance stance, float adsBlend, float speedMultiplier, float forcedWalk)
        {
            float v;
            switch (stance)
            {
                case Stance.Sprint:
                    v = s.SprintSpeed;
                    break;
                case Stance.Crouch:
                    v = Mathf.Min(s.CrouchSpeed, Mathf.Lerp(s.WalkSpeed, s.AdsWalkSpeed, Mathf.Clamp01(adsBlend)));
                    break;
                default:
                    v = Mathf.Lerp(s.WalkSpeed, s.AdsWalkSpeed, Mathf.Clamp01(adsBlend));
                    break;
            }
            v *= Mathf.Clamp(speedMultiplier, 0.1f, 2f);
            if (forcedWalk > 0f) v = Mathf.Min(v, forcedWalk);
            return v;
        }

        /// <summary>Speed factor for the input direction: 1 forward/strafe, <paramref name="backpedal"/> straight back.</summary>
        public static float DirectionalFactor(Vector2 move, float backpedal)
        {
            var m = move.magnitude;
            if (m < 1e-4f || move.y >= 0f) return 1f;
            return Mathf.Lerp(1f, backpedal, -move.y / m);
        }

        /// <summary>World-space wish direction for WASD in a heading frame (magnitude = input magnitude, &lt;= 1).</summary>
        public static Vector3 WishDirection(Vector2 move, float headingDegrees)
        {
            move = Vector2.ClampMagnitude(move, 1f);
            var r = headingDegrees * Mathf.Deg2Rad;
            var sin = Mathf.Sin(r);
            var cos = Mathf.Cos(r);
            // forward = (sin, 0, cos), right = (cos, 0, -sin)
            return new Vector3(move.y * sin + move.x * cos, 0f, move.y * cos - move.x * sin);
        }

        /// <summary>Horizontal unit vector of a compass heading (0 = +Z, 90 = +X).</summary>
        public static Vector3 Forward(float headingDegrees) => MathX.DirectionFromBearing(headingDegrees);

        /// <summary>
        /// Moves a planar velocity toward a target: <see cref="MotorSettings.GroundAcceleration"/> up to walk speed,
        /// <see cref="MotorSettings.SprintAcceleration"/> above it, <see cref="MotorSettings.GroundDeceleration"/> when
        /// slowing down or turning away.
        /// </summary>
        public static Vector3 Accelerate(Vector3 current, Vector3 target, MotorSettings s, float speedMultiplier, float dt)
        {
            current.y = 0f;
            target.y = 0f;
            var cur2 = current.sqrMagnitude;
            var tgt2 = target.sqrMagnitude;
            float rate;
            if (tgt2 + 1e-6f < cur2 || Vector3.Dot(current, target) < 0f) rate = s.GroundDeceleration;
            else if (cur2 >= Sqr(s.WalkSpeed * Mathf.Clamp(speedMultiplier, 0.1f, 2f)) * 0.98f) rate = s.SprintAcceleration;
            else rate = s.GroundAcceleration;
            return Vector3.MoveTowards(current, target, rate * dt);
        }

        /// <summary>
        /// Air control: steers toward <paramref name="wish"/> × <paramref name="maxSpeed"/> at <paramref name="accel"/>;
        /// with no input the momentum is kept (no air friction).
        /// </summary>
        public static Vector3 AirAccelerate(Vector3 current, Vector3 wish, float maxSpeed, float accel, float dt)
        {
            current.y = 0f;
            if (wish.sqrMagnitude < 1e-4f) return current;
            var target = wish * maxSpeed;
            return Vector3.MoveTowards(current, target, accel * dt);
        }

        /// <summary>Launch speed for a jump apex height: v = sqrt(2 g h) (GDD §12: apex 1.0 m).</summary>
        public static float JumpVelocity(float apexHeight, float gravity) => Mathf.Sqrt(2f * Mathf.Max(0f, gravity) * Mathf.Max(0f, apexHeight));

        /// <summary>Apex height of a launch speed: h = v² / 2g.</summary>
        public static float ApexHeight(float launchSpeed, float gravity) => gravity <= 0f ? 0f : launchSpeed * launchSpeed / (2f * gravity);

        /// <summary>
        /// Exact vertical displacement over <paramref name="dt"/> under constant gravity (no Euler error, so the jump
        /// apex is exactly 1.0 m at any frame rate). Returns the displacement and updates the velocity.
        /// </summary>
        public static float IntegrateVertical(ref float velocity, float gravity, float terminal, float dt)
        {
            var dy = (velocity - 0.5f * gravity * dt) * dt;
            velocity -= gravity * dt;
            if (velocity < -terminal)
            {
                velocity = -terminal;
                dy = Mathf.Max(dy, -terminal * dt);
            }
            return dy;
        }

        /// <summary>True when a slide can start: grounded at &gt;= 5.5 m/s (GDD §12).</summary>
        public static bool CanStartSlide(float planarSpeed, MotorSettings s) => planarSpeed >= s.SlideEntrySpeed - 1e-3f;

        /// <summary>
        /// Slide speed after <paramref name="t"/> seconds (GDD §12: 8.0 → 4.0 m/s over 0.75 s). Ease-out (friction bites
        /// hardest at the start), exactly 8.0 at t = 0 and 4.0 at the end, monotonic.
        /// </summary>
        public static float SlideSpeed(MotorSettings s, float t)
        {
            var x = s.SlideDuration > 0f ? Mathf.Clamp01(t / s.SlideDuration) : 1f;
            var u = 1f - Mathf.Pow(1f - x, 1.6f); // 0 at x = 0, 1 at x = 1, steepest at the start
            return Mathf.Lerp(s.SlideStartSpeed, s.SlideEndSpeed, u);
        }

        /// <summary>Fall damage in HP for a fall height (GDD §12: none below 4.5 m, then 30 HP per metre).</summary>
        public static float FallDamage(float fallHeight, MotorSettings s) => Mathf.Max(0f, fallHeight - s.SafeFallHeight) * s.FallDamagePerMetre;

        /// <summary>
        /// Ground traversal for a ledge top height above the feet (GDD §12): &lt; 0.6 none (step or jump), 0.6–1.4 vault
        /// (1.4 inclusive), above 1.4 up to 2.1 climb, higher none.
        /// </summary>
        public static LedgeKind ClassifyGroundLedge(float topHeight, MotorSettings s)
        {
            if (topHeight < s.VaultMinHeight - 1e-4f) return LedgeKind.None;
            if (topHeight <= s.VaultMaxHeight + 1e-4f) return LedgeKind.Vault;
            if (topHeight <= s.ClimbMaxHeight + 1e-4f) return LedgeKind.Climb;
            return LedgeKind.None;
        }

        /// <summary>
        /// Air mantle test (GDD §12): ledge top 0–1.4 m above the feet (from <see cref="MotorSettings.AirMantleMinAboveFeet"/>)
        /// and the face &lt;= 0.8 m ahead of the body centre.
        /// </summary>
        public static bool IsAirMantle(float topAboveFeet, float faceDistance, MotorSettings s) =>
            topAboveFeet >= s.AirMantleMinAboveFeet - 1e-4f && topAboveFeet <= s.AirMantleMaxAboveFeet + 1e-4f &&
            faceDistance <= s.AirMantleReach + 1e-4f;

        /// <summary>Duration of a traversal (GDD §12: vault 0.45 s, climb 0.8 s, air mantle 0.5 s).</summary>
        public static float TraversalDuration(LedgeKind kind, MotorSettings s)
        {
            switch (kind)
            {
                case LedgeKind.Vault: return s.VaultDuration;
                case LedgeKind.Climb: return s.ClimbDuration;
                case LedgeKind.AirMantle: return s.AirMantleDuration;
                case LedgeKind.LadderExit: return s.LadderExitDuration;
                default: return 0f;
            }
        }

        /// <summary>Part of the traversal spent rising before moving over the top.</summary>
        public static float TraversalRiseFraction(LedgeKind kind)
        {
            switch (kind)
            {
                case LedgeKind.Vault: return 0.45f;
                case LedgeKind.Climb: return 0.62f;
                case LedgeKind.AirMantle: return 0.55f;
                case LedgeKind.LadderExit: return 0.5f;
                default: return 0.5f;
            }
        }

        /// <summary>Eye height for a stance (GDD §12: 1.65 standing, 1.05 crouched; slide dips 0.6 m).</summary>
        public static float EyeHeight(Stance stance, bool crouched, MotorSettings s)
        {
            switch (stance)
            {
                case Stance.Slide: return s.StandEyeHeight - s.SlideCameraDip;
                case Stance.Crouch: return s.CrouchEyeHeight;
                default: return crouched ? s.CrouchEyeHeight : s.StandEyeHeight;
            }
        }

        /// <summary>Capsule height (GDD §12: 1.8 m; TECH_PLAN §7.3: crouch 1.1 m).</summary>
        public static float CapsuleHeight(bool crouched, MotorSettings s) => crouched ? s.CrouchHeight : s.StandHeight;

        /// <summary>True for a surface normal within the slope limit.</summary>
        public static bool IsWalkable(Vector3 normal, float slopeLimit) => Vector3.Angle(normal, Vector3.up) <= slopeLimit + 0.5f;

        /// <summary>Sprint needs a mostly forward input (W, W+A, W+D).</summary>
        public static bool IsSprintInput(Vector2 move) => move.y > 0.5f && Mathf.Abs(move.x) <= move.y + 0.01f;

        private static float Sqr(float x) => x * x;
    }
}
